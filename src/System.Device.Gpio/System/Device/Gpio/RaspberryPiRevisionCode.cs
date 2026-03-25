// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace System.Device.Gpio;

/// <summary>
/// Raspberry Pi revision code parser for old-style and new-style format.
/// </summary>
internal sealed class RaspberryPiRevisionCode
{
    private const int NewStyleFlag = 1 << 23;
    private const int WarrantyBit24Mask = 1 << 24;
    private const int WarrantyBit25Mask = 1 << 25;

    private RaspberryPiRevisionCode(int rawValue)
    {
        RawValue = rawValue;
        IsNewStyle = (rawValue & NewStyleFlag) != 0;
        IsWarrantyBit24Set = (rawValue & WarrantyBit24Mask) != 0;
        IsWarrantyBit25Set = (rawValue & WarrantyBit25Mask) != 0;

        if (IsNewStyle)
        {
            BoardRevision = rawValue & 0xF;
            BoardTypeCode = (rawValue >> 4) & 0xFF;
            Processor = DecodeProcessor((rawValue >> 12) & 0xF);
            Manufacturer = DecodeManufacturer((rawValue >> 16) & 0xF);
            MemorySize = DecodeMemorySize((rawValue >> 20) & 0x7);
            BoardModel = DecodeNewStyleModel(BoardTypeCode);
        }
        else
        {
            BoardRevision = rawValue & 0xF;
            BoardTypeCode = rawValue & 0xFFFF;
            Processor = ProcessorType.Unknown;
            Manufacturer = ManufacturerType.Unknown;
            MemorySize = MemorySizeType.Unknown;
            BoardModel = DecodeOldStyleModel(rawValue & 0xFFFF);
        }
    }

    internal enum ProcessorType
    {
        Unknown,
        Bcm2835,
        Bcm2836,
        Bcm2837,
        Bcm2711,
        Bcm2712,
    }

    internal enum ManufacturerType
    {
        Unknown,
        SonyUk,
        Egoman,
        Embest,
        SonyJapan,
        Embest2,
        Stadium,
    }

    internal enum MemorySizeType
    {
        Unknown,
        Mb256,
        Mb512,
        Gb1,
        Gb2,
        Gb4,
        Gb8,
        Gb16,
    }

    internal int RawValue { get; }

    internal bool IsNewStyle { get; }

    internal bool IsWarrantyBit24Set { get; }

    internal bool IsWarrantyBit25Set { get; }

    internal bool IsOverclocked => (RawValue & unchecked((int)0xFFFF0000)) != 0;

    internal int BoardRevision { get; }

    internal int BoardTypeCode { get; }

    internal ProcessorType Processor { get; }

    internal ManufacturerType Manufacturer { get; }

    internal MemorySizeType MemorySize { get; }

    internal RaspberryBoardInfo.Model BoardModel { get; }

    internal static bool TryParse(string revisionCode, out RaspberryPiRevisionCode? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(revisionCode))
        {
            return false;
        }

        if (!int.TryParse(revisionCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int firmware))
        {
            return false;
        }

        parsed = new RaspberryPiRevisionCode(firmware);
        return true;
    }

    internal static RaspberryPiRevisionCode Parse(int rawValue)
    {
        return new RaspberryPiRevisionCode(rawValue);
    }

    private static ProcessorType DecodeProcessor(int code) => code switch
    {
        0 => ProcessorType.Bcm2835,
        1 => ProcessorType.Bcm2836,
        2 => ProcessorType.Bcm2837,
        3 => ProcessorType.Bcm2711,
        4 => ProcessorType.Bcm2712,
        _ => ProcessorType.Unknown,
    };

    private static ManufacturerType DecodeManufacturer(int code) => code switch
    {
        0 => ManufacturerType.SonyUk,
        1 => ManufacturerType.Egoman,
        2 => ManufacturerType.Embest,
        3 => ManufacturerType.SonyJapan,
        4 => ManufacturerType.Embest2,
        5 => ManufacturerType.Stadium,
        _ => ManufacturerType.Unknown,
    };

    private static MemorySizeType DecodeMemorySize(int code) => code switch
    {
        0 => MemorySizeType.Mb256,
        1 => MemorySizeType.Mb512,
        2 => MemorySizeType.Gb1,
        3 => MemorySizeType.Gb2,
        4 => MemorySizeType.Gb4,
        5 => MemorySizeType.Gb8,
        6 => MemorySizeType.Gb16,
        _ => MemorySizeType.Unknown,
    };

    private static RaspberryBoardInfo.Model DecodeNewStyleModel(int boardTypeCode) => boardTypeCode switch
    {
        0x00 => RaspberryBoardInfo.Model.RaspberryPiA,
        0x01 => RaspberryBoardInfo.Model.RaspberryPiBRev2,
        0x02 => RaspberryBoardInfo.Model.RaspberryPiAPlus,
        0x03 => RaspberryBoardInfo.Model.RaspberryPiBPlus,
        0x04 => RaspberryBoardInfo.Model.RaspberryPi2B,
        0x06 => RaspberryBoardInfo.Model.RaspberryPiComputeModule,
        0x08 => RaspberryBoardInfo.Model.RaspberryPi3B,
        0x09 => RaspberryBoardInfo.Model.RaspberryPiZero,
        0x0A => RaspberryBoardInfo.Model.RaspberryPiComputeModule3,
        0x0C => RaspberryBoardInfo.Model.RaspberryPiZeroW,
        0x0D => RaspberryBoardInfo.Model.RaspberryPi3BPlus,
        0x0E => RaspberryBoardInfo.Model.RaspberryPi3APlus,
        0x11 => RaspberryBoardInfo.Model.RaspberryPi4,
        0x12 => RaspberryBoardInfo.Model.RaspberryPiZero2W,
        0x13 => RaspberryBoardInfo.Model.RaspberryPi400,
        0x14 => RaspberryBoardInfo.Model.RaspberryPiComputeModule4,
        0x15 => RaspberryBoardInfo.Model.RaspberryPiComputeModule4,
        0x17 => RaspberryBoardInfo.Model.RaspberryPi5,
        0x18 => RaspberryBoardInfo.Model.RaspberryPiComputeModule5,
        0x19 => RaspberryBoardInfo.Model.RaspberryPi5,
        0x1A => RaspberryBoardInfo.Model.RaspberryPiComputeModule5Lite,
        _ => RaspberryBoardInfo.Model.Unknown,
    };

    private static RaspberryBoardInfo.Model DecodeOldStyleModel(int boardTypeCode) => boardTypeCode switch
    {
        0x2 or 0x3 => RaspberryBoardInfo.Model.RaspberryPiBRev1,
        0x4 or 0x5 or 0x6 or 0xD or 0xE or 0xF => RaspberryBoardInfo.Model.RaspberryPiBRev2,
        0x7 or 0x8 or 0x9 => RaspberryBoardInfo.Model.RaspberryPiA,
        0x10 or 0x13 or 0x32 => RaspberryBoardInfo.Model.RaspberryPiBPlus,
        0x11 or 0x14 or 0x61 => RaspberryBoardInfo.Model.RaspberryPiComputeModule,
        0x12 or 0x15 or 0x21 => RaspberryBoardInfo.Model.RaspberryPiAPlus,
        0x1040 or 0x1041 or 0x2042 => RaspberryBoardInfo.Model.RaspberryPi2B,
        0x92 or 0x93 => RaspberryBoardInfo.Model.RaspberryPiZero,
        0xC1 => RaspberryBoardInfo.Model.RaspberryPiZeroW,
        0x2120 => RaspberryBoardInfo.Model.RaspberryPiZero2W,
        0x2082 or 0x2083 => RaspberryBoardInfo.Model.RaspberryPi3B,
        0x20D3 or 0x20D4 => RaspberryBoardInfo.Model.RaspberryPi3BPlus,
        0x20E0 or 0x20E1 => RaspberryBoardInfo.Model.RaspberryPi3APlus,
        0x20A0 or 0x2100 => RaspberryBoardInfo.Model.RaspberryPiComputeModule3,
        0x3111 or 0x3112 or 0x3114 or 0x3115 => RaspberryBoardInfo.Model.RaspberryPi4,
        0x3140 or 0x3141 => RaspberryBoardInfo.Model.RaspberryPiComputeModule4,
        0x3130 or 0x3131 => RaspberryBoardInfo.Model.RaspberryPi400,
        0x4170 => RaspberryBoardInfo.Model.RaspberryPi5,
        0x4180 => RaspberryBoardInfo.Model.RaspberryPiComputeModule5,
        0x41A0 => RaspberryBoardInfo.Model.RaspberryPiComputeModule5Lite,
        _ => RaspberryBoardInfo.Model.Unknown,
    };
}
