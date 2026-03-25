// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Interop;

namespace System.Device.Gpio.Drivers;

/// <summary>
/// A GPIO driver for Raspberry Pi 5, Raspberry Pi Compute Module 5 and Raspberry Pi Compute Module 5 Lite.
/// This driver directly accesses the RP1 GPIO registers through <c>/dev/gpiomem0</c>.
/// </summary>
public unsafe class RaspberryPi5Driver : GpioDriver
{
    private const int ENOENT = 2;
    private const string Rp1GpioMemoryFilePath = "/dev/gpiomem0";
    private const int Rp1RegisterMapLength = 0x30000;
    private const int Rp1NumberOfGpios = 54;

    private const int Rp1Bank0BasePin = 0;
    private const int Rp1Bank1BasePin = 28;
    private const int Rp1Bank2BasePin = 34;

    private const uint Rp1IoBank0Offset = 0x0000_0000;
    private const uint Rp1IoBank1Offset = 0x0000_4000;
    private const uint Rp1IoBank2Offset = 0x0000_8000;
    private const uint Rp1SysRioBank0Offset = 0x0001_0000;
    private const uint Rp1SysRioBank1Offset = 0x0001_4000;
    private const uint Rp1SysRioBank2Offset = 0x0001_8000;
    private const uint Rp1PadsBank0Offset = 0x0002_0000;
    private const uint Rp1PadsBank1Offset = 0x0002_4000;
    private const uint Rp1PadsBank2Offset = 0x0002_8000;

    private const uint Rp1AtomicSetOffset = 0x2000;
    private const uint Rp1AtomicClearOffset = 0x3000;

    private const uint Rp1GpioCtrlFuncSelMask = 0x1F;
    private const uint Rp1GpioFuncSelSysRio = 0x05;

    private const uint Rp1PadsOutputDisableBit = 1U << 7;
    private const uint Rp1PadsInputEnableBit = 1U << 6;
    private const uint Rp1PadsPullUpEnableBit = 1U << 3;
    private const uint Rp1PadsPullDownEnableBit = 1U << 2;

    private const uint Rp1SysRioOutOffset = 0x0;
    private const uint Rp1SysRioOeOffset = 0x4;
    private const uint Rp1SysRioSyncInOffset = 0x8;

    private static readonly object s_initializationLock = new object();

    private readonly PinState?[] _pinModes;
    private uint* _registers;
    private GpioDriver? _interruptDriver;
    private int? _interruptChipId;

    /// <summary>
    /// Creates an instance of the Raspberry Pi 5 RP1 GPIO driver.
    /// </summary>
    public RaspberryPi5Driver()
    {
        if (Environment.OSVersion.Platform != PlatformID.Unix)
        {
            throw new PlatformNotSupportedException($"{nameof(RaspberryPi5Driver)} is only supported on Linux/Unix");
        }

        _pinModes = new PinState[PinCount];
    }

    /// <inheritdoc />
    protected internal override int PinCount => Rp1NumberOfGpios;

    /// <inheritdoc />
    protected internal override void OpenPin(int pinNumber)
    {
        ValidatePinNumber(pinNumber);
        Initialize();
        GetPinModeFromHardware(pinNumber);
    }

    /// <inheritdoc />
    protected internal override void ClosePin(int pinNumber)
    {
        ValidatePinNumber(pinNumber);

        if (_pinModes[pinNumber]?.InUseByInterruptDriver ?? false)
        {
            _interruptDriver!.ClosePin(pinNumber);
        }

        _pinModes[pinNumber] = null;
    }

    /// <inheritdoc />
    protected internal override void SetPinMode(int pinNumber, PinMode mode)
    {
        ValidatePinNumber(pinNumber);
        Initialize();

        if (!IsPinModeSupported(pinNumber, mode))
        {
            throw new InvalidOperationException($"The pin {pinNumber} does not support the selected mode {mode}.");
        }

        GetBankAndOffset(pinNumber, out int bank, out int offsetInBank);
        uint ioCtrlOffset = GetIoRegisterOffset(bank, offsetInBank, true);
        uint padsOffset = GetPadsRegisterOffset(bank, offsetInBank);
        uint sysRioOeSetOffset = GetSysRioRegisterOffset(bank, Rp1SysRioOeOffset + Rp1AtomicSetOffset);
        uint sysRioOeClearOffset = GetSysRioRegisterOffset(bank, Rp1SysRioOeOffset + Rp1AtomicClearOffset);

        uint ctrlReg = ReadRegister(ioCtrlOffset);
        ctrlReg &= ~Rp1GpioCtrlFuncSelMask;
        ctrlReg |= Rp1GpioFuncSelSysRio;
        WriteRegister(ioCtrlOffset, ctrlReg);

        uint padsReg = ReadRegister(padsOffset);
        padsReg |= Rp1PadsInputEnableBit;
        padsReg &= ~Rp1PadsOutputDisableBit;
        padsReg &= ~(Rp1PadsPullUpEnableBit | Rp1PadsPullDownEnableBit);

        if (mode == PinMode.InputPullUp)
        {
            padsReg |= Rp1PadsPullUpEnableBit;
        }
        else if (mode == PinMode.InputPullDown)
        {
            padsReg |= Rp1PadsPullDownEnableBit;
        }

        WriteRegister(padsOffset, padsReg);

        if (mode == PinMode.Output)
        {
            WriteRegister(sysRioOeSetOffset, 1U << offsetInBank);
        }
        else
        {
            WriteRegister(sysRioOeClearOffset, 1U << offsetInBank);
        }

        if (_pinModes[pinNumber] is not null)
        {
            _pinModes[pinNumber]!.CurrentPinMode = mode;
        }
        else
        {
            _pinModes[pinNumber] = new PinState(mode);
        }
    }

    /// <inheritdoc />
    protected internal override void SetPinMode(int pinNumber, PinMode mode, PinValue initialValue)
    {
        Write(pinNumber, initialValue);
        SetPinMode(pinNumber, mode);
    }

    /// <inheritdoc />
    protected internal override PinMode GetPinMode(int pinNumber)
    {
        ValidatePinNumber(pinNumber);

        var entry = _pinModes[pinNumber];
        if (entry == null)
        {
            throw new InvalidOperationException("Can not get a pin mode of a pin that is not open.");
        }

        return entry.CurrentPinMode;
    }

    /// <inheritdoc />
    protected internal override bool IsPinModeSupported(int pinNumber, PinMode mode)
    {
        ValidatePinNumber(pinNumber);
        return mode is PinMode.Input or PinMode.Output or PinMode.InputPullDown or PinMode.InputPullUp;
    }

    /// <inheritdoc />
    protected internal override PinValue Read(int pinNumber)
    {
        ValidatePinNumber(pinNumber);
        Initialize();

        GetBankAndOffset(pinNumber, out int bank, out int offsetInBank);
        uint register = ReadRegister(GetSysRioRegisterOffset(bank, Rp1SysRioSyncInOffset));
        return ((register >> offsetInBank) & 1U) == 1U ? PinValue.High : PinValue.Low;
    }

    /// <inheritdoc />
    protected internal override void Write(int pinNumber, PinValue value)
    {
        ValidatePinNumber(pinNumber);
        Initialize();

        GetBankAndOffset(pinNumber, out int bank, out int offsetInBank);
        uint registerOffset = value == PinValue.High
            ? GetSysRioRegisterOffset(bank, Rp1SysRioOutOffset + Rp1AtomicSetOffset)
            : GetSysRioRegisterOffset(bank, Rp1SysRioOutOffset + Rp1AtomicClearOffset);
        WriteRegister(registerOffset, 1U << offsetInBank);
    }

    /// <inheritdoc />
    protected internal override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
    {
        ValidatePinNumber(pinNumber);
        InitializeInterruptDriver();
        EnsurePinPreparedForInterrupts(pinNumber);
        return _interruptDriver.WaitForEvent(pinNumber, eventTypes, cancellationToken);
    }

    /// <inheritdoc />
    protected internal override ValueTask<WaitForEventResult> WaitForEventAsync(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
    {
        ValidatePinNumber(pinNumber);
        InitializeInterruptDriver();
        EnsurePinPreparedForInterrupts(pinNumber);
        return _interruptDriver.WaitForEventAsync(pinNumber, eventTypes, cancellationToken);
    }

    /// <inheritdoc />
    protected internal override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
    {
        ValidatePinNumber(pinNumber);
        InitializeInterruptDriver();
        EnsurePinPreparedForInterrupts(pinNumber);
        _interruptDriver.AddCallbackForPinValueChangedEvent(pinNumber, eventTypes, callback);
    }

    /// <inheritdoc />
    protected internal override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
    {
        ValidatePinNumber(pinNumber);
        InitializeInterruptDriver();
        EnsurePinPreparedForInterrupts(pinNumber);
        _interruptDriver.RemoveCallbackForPinValueChangedEvent(pinNumber, callback);
    }

    /// <inheritdoc />
    public override GpioChipInfo GetChipInfo()
    {
        if (_interruptChipId.HasValue)
        {
            return new GpioChipInfo(_interruptChipId.Value, nameof(RaspberryPi5Driver), "RP1", PinCount);
        }

        return new GpioChipInfo(0, nameof(RaspberryPi5Driver), "RP1", PinCount);
    }

    /// <inheritdoc />
    public override ComponentInformation QueryComponentInformation()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Raspberry Pi 5 RP1 GPIO driver with ");
        sb.Append(PinCount);
        sb.Append(" pins");
        if (_interruptDriver != null)
        {
            sb.Append(" and an interrupt driver");
        }

        ComponentInformation ci = new ComponentInformation(this, sb.ToString());
        ci.Properties["MemoryMappedDevice"] = Rp1GpioMemoryFilePath;
        if (_interruptDriver != null)
        {
            ci.AddSubComponent(_interruptDriver.QueryComponentInformation());
        }

        return ci;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_registers != null)
        {
            Interop.munmap((IntPtr)_registers, Rp1RegisterMapLength);
            _registers = null;
        }

        _interruptDriver?.Dispose();
        _interruptDriver = null;
    }

    private void ValidatePinNumber(int pinNumber)
    {
        if (pinNumber < 0 || pinNumber >= PinCount)
        {
            throw new ArgumentException("The specified pin number is invalid.", nameof(pinNumber));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadRegister(uint registerOffset, uint* registerBase)
    {
        return registerBase[registerOffset / 4];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint ReadRegister(uint registerOffset)
    {
        return ReadRegister(registerOffset, _registers);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteRegister(uint registerOffset, uint value)
    {
        _registers[registerOffset / 4] = value;
    }

    private static uint GetIoRegisterOffset(int bank, int offsetInBank, bool controlRegister)
    {
        uint ioBankBase = bank switch
        {
            0 => Rp1IoBank0Offset,
            1 => Rp1IoBank1Offset,
            2 => Rp1IoBank2Offset,
            _ => throw new ArgumentOutOfRangeException(nameof(bank))
        };

        uint registerOffsetForPin = (uint)(((offsetInBank * 2) + (controlRegister ? 1 : 0)) * sizeof(uint));
        return ioBankBase + registerOffsetForPin;
    }

    private static uint GetPadsRegisterOffset(int bank, int offsetInBank)
    {
        uint padsBankBase = bank switch
        {
            0 => Rp1PadsBank0Offset,
            1 => Rp1PadsBank1Offset,
            2 => Rp1PadsBank2Offset,
            _ => throw new ArgumentOutOfRangeException(nameof(bank))
        };

        return padsBankBase + sizeof(uint) + ((uint)offsetInBank * sizeof(uint));
    }

    private static uint GetSysRioRegisterOffset(int bank, uint registerOffset)
    {
        uint sysRioBankBase = bank switch
        {
            0 => Rp1SysRioBank0Offset,
            1 => Rp1SysRioBank1Offset,
            2 => Rp1SysRioBank2Offset,
            _ => throw new ArgumentOutOfRangeException(nameof(bank))
        };

        return sysRioBankBase + registerOffset;
    }

    private static void GetBankAndOffset(int pinNumber, out int bank, out int offsetInBank)
    {
        if (pinNumber < Rp1Bank1BasePin)
        {
            bank = 0;
            offsetInBank = pinNumber - Rp1Bank0BasePin;
        }
        else if (pinNumber < Rp1Bank2BasePin)
        {
            bank = 1;
            offsetInBank = pinNumber - Rp1Bank1BasePin;
        }
        else
        {
            bank = 2;
            offsetInBank = pinNumber - Rp1Bank2BasePin;
        }
    }

    private PinMode GetPinModeFromHardware(int pinNumber)
    {
        GetBankAndOffset(pinNumber, out int bank, out int offsetInBank);

        uint ctrl = ReadRegister(GetIoRegisterOffset(bank, offsetInBank, true));
        bool sysRioSelected = (ctrl & Rp1GpioCtrlFuncSelMask) == Rp1GpioFuncSelSysRio;
        PinMode mode;

        if (!sysRioSelected)
        {
            mode = PinMode.Input;
        }
        else
        {
            uint oe = ReadRegister(GetSysRioRegisterOffset(bank, Rp1SysRioOeOffset));
            bool isOutput = ((oe >> offsetInBank) & 1U) == 1U;
            if (isOutput)
            {
                mode = PinMode.Output;
            }
            else
            {
                uint pads = ReadRegister(GetPadsRegisterOffset(bank, offsetInBank));
                bool pullUp = (pads & Rp1PadsPullUpEnableBit) != 0;
                bool pullDown = (pads & Rp1PadsPullDownEnableBit) != 0;
                mode = pullUp ? PinMode.InputPullUp : pullDown ? PinMode.InputPullDown : PinMode.Input;
            }
        }

        if (_pinModes[pinNumber] is not null)
        {
            _pinModes[pinNumber]!.CurrentPinMode = mode;
        }
        else
        {
            _pinModes[pinNumber] = new PinState(mode);
        }

        return mode;
    }

    private void InitializeInterruptDriver()
    {
        if (_interruptDriver != null)
        {
            return;
        }

        IList<GpioChipInfo> chips;
        try
        {
            chips = LibGpiodDriver.GetAvailableChips();
        }
        catch (Exception x) when (x is DllNotFoundException || x is PlatformNotSupportedException)
        {
            chips = Array.Empty<GpioChipInfo>();
        }

        GpioChipInfo? selectedChip = chips.FirstOrDefault(x => x.NumLines == PinCount);
        if (selectedChip != null)
        {
            _interruptChipId = selectedChip.Id;
            if (GpioDriver.TryCreate(() => (GpioDriver)new LibGpiodDriver(selectedChip.Id), out GpioDriver? gpioDriver))
            {
                _interruptDriver = gpioDriver;
                return;
            }

            if (GpioDriver.TryCreate(() => (GpioDriver)new LibGpiodV2Driver(selectedChip.Id), out GpioDriver? gpioDriverV2))
            {
                _interruptDriver = gpioDriverV2;
                return;
            }
        }

        _interruptDriver = new InterruptSysFsDriver(this);
    }

    private void Initialize()
    {
        if (_registers != null)
        {
            return;
        }

        lock (s_initializationLock)
        {
            if (_registers != null)
            {
                return;
            }

            int fileDescriptor = Interop.open(Rp1GpioMemoryFilePath, FileOpenFlags.O_RDWR | FileOpenFlags.O_SYNC);
            if (fileDescriptor == -1)
            {
                int win32Error = Marshal.GetLastWin32Error();
                string errorMessage = Marshal.GetLastPInvokeErrorMessage();
                if (win32Error == ENOENT)
                {
                    throw new PlatformNotSupportedException($"{Rp1GpioMemoryFilePath} is not available. This driver requires Raspberry Pi 5 RP1 GPIO access. Ensure the system is running on Raspberry Pi 5/CM5 hardware with a kernel exposing /dev/gpiomem0 and with sufficient permissions.");
                }

                string error = string.IsNullOrWhiteSpace(errorMessage) ? win32Error.ToString() : $"{win32Error} ({errorMessage})";
                throw new IOException($"Error {error} initializing the GPIO driver.");
            }

            IntPtr mapPointer = Interop.mmap(IntPtr.Zero, Rp1RegisterMapLength, MemoryMappedProtections.PROT_READ | MemoryMappedProtections.PROT_WRITE, MemoryMappedFlags.MAP_SHARED, fileDescriptor, 0);
            Interop.close(fileDescriptor);
            if (mapPointer.ToInt64() == -1)
            {
                throw new IOException($"Error {Marshal.GetLastPInvokeErrorMessage()} initializing the GPIO driver.");
            }

            _registers = (uint*)mapPointer;
        }
    }

    private void EnsurePinPreparedForInterrupts(int pinNumber)
    {
        _interruptDriver!.OpenPin(pinNumber);
        if (_pinModes[pinNumber] is null)
        {
            _pinModes[pinNumber] = new PinState(GetPinModeFromHardware(pinNumber));
        }

        _pinModes[pinNumber]!.InUseByInterruptDriver = true;
    }

    private class PinState
    {
        public PinState(PinMode currentMode)
        {
            CurrentPinMode = currentMode;
        }

        public PinMode CurrentPinMode { get; set; }

        public bool InUseByInterruptDriver { get; set; }
    }
}
