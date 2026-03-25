// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

namespace System.Device.Gpio;

/// <summary>
/// Identification of Raspberry Pi board models
/// </summary>
internal class RaspberryBoardInfo
{
    /// <summary>
    /// The Raspberry Pi model.
    /// </summary>
    public enum Model
    {
        /// <summary>
        /// Unknown model.
        /// </summary>
        Unknown,

        /// <summary>
        /// Raspberry Model A.
        /// </summary>
        RaspberryPiA,

        /// <summary>
        /// Model A+.
        /// </summary>
        RaspberryPiAPlus,

        /// <summary>
        /// Model B rev1.
        /// </summary>
        RaspberryPiBRev1,

        /// <summary>
        /// Model B rev2.
        /// </summary>
        RaspberryPiBRev2,

        /// <summary>
        /// Model B+.
        /// </summary>
        RaspberryPiBPlus,

        /// <summary>
        /// Compute module.
        /// </summary>
        RaspberryPiComputeModule,

        /// <summary>
        /// Pi 2 Model B.
        /// </summary>
        RaspberryPi2B,

        /// <summary>
        /// Pi Zero.
        /// </summary>
        RaspberryPiZero,

        /// <summary>
        /// Pi Zero W.
        /// </summary>
        RaspberryPiZeroW,

        /// <summary>
        /// Pi Zero 2 W.
        /// </summary>
        RaspberryPiZero2W,

        /// <summary>
        /// Pi 3 Model B.
        /// </summary>
        RaspberryPi3B,

        /// <summary>
        /// Pi 3 Model A+.
        /// </summary>
        RaspberryPi3APlus,

        /// <summary>
        /// Pi 3 Model B+.
        /// </summary>
        RaspberryPi3BPlus,

        /// <summary>
        /// Compute module 3.
        /// </summary>
        RaspberryPiComputeModule3,

        /// <summary>
        /// Pi 4 all versions.
        /// </summary>
        RaspberryPi4,

        /// <summary>
        /// Pi 400
        /// </summary>
        RaspberryPi400,

        /// <summary>
        /// Compute module 4.
        /// </summary>
        RaspberryPiComputeModule4,

        /// <summary>
        /// Pi 5 Model B+
        /// </summary>
        RaspberryPi5,

        /// <summary>
        /// Compute module 5.
        /// </summary>
        RaspberryPiComputeModule5,

        /// <summary>
        /// Compute module 5 Lite (no eMMC).
        /// </summary>
        RaspberryPiComputeModule5Lite,
    }

    #region Fields

    private readonly Dictionary<string, string> _settings;
    private readonly RaspberryPiRevisionCode _revisionCode;

    private RaspberryBoardInfo(Dictionary<string, string> settings)
    {
        _settings = settings;

        ProcessorName = _settings.TryGetValue("Hardware", out string? hardware) && hardware is object ? hardware : string.Empty;

        if (_settings.TryGetValue("Revision", out string? revision)
            && RaspberryPiRevisionCode.TryParse(revision, out RaspberryPiRevisionCode? parsedRevisionCode)
            && parsedRevisionCode is not null)
        {
            _revisionCode = parsedRevisionCode;
            Firmware = _revisionCode.RawValue;
        }
        else
        {
            _revisionCode = RaspberryPiRevisionCode.Parse(0);
        }

        if (_settings.TryGetValue("Serial", out string? serial))
        {
            SerialNumber = serial;
        }

        BoardModel = _revisionCode.BoardModel;
        IsNewStyleRevisionCode = _revisionCode.IsNewStyle;
    }

    #endregion

    #region Properties

    public Model BoardModel
    {
        get;
    }

    /// <summary>
    /// Gets whether the firmware revision is represented using the new-style revision code format.
    /// </summary>
    public bool IsNewStyleRevisionCode
    {
        get;
    }

    /// <summary>
    /// Gets the revision field from the revision code.
    /// </summary>
    public int BoardRevision => _revisionCode.BoardRevision;

    /// <summary>
    /// Gets the board type code from the revision code.
    /// </summary>
    public int BoardTypeCode => _revisionCode.BoardTypeCode;

    /// <summary>
    /// Gets the board processor decoded from the revision code.
    /// </summary>
    public RaspberryPiRevisionCode.ProcessorType BoardProcessor => _revisionCode.Processor;

    /// <summary>
    /// Gets the board manufacturer decoded from the revision code.
    /// </summary>
    public RaspberryPiRevisionCode.ManufacturerType BoardManufacturer => _revisionCode.Manufacturer;

    /// <summary>
    /// Gets the board memory size decoded from the revision code.
    /// </summary>
    public RaspberryPiRevisionCode.MemorySizeType BoardMemorySize => _revisionCode.MemorySize;

    /// <summary>
    /// Gets the processor name.
    /// </summary>
    /// <value>
    /// The name of the processor.
    /// </value>
    public string ProcessorName
    {
        get;
    }

    /// <summary>
    /// Gets the board firmware version.
    /// </summary>
    public int Firmware
    {
        get;
    }

    /// <summary>
    /// Gets the serial number.
    /// </summary>
    public string? SerialNumber
    {
        get;
    }

    /// <summary>
    /// Gets a value indicating whether board is overclocked.
    /// </summary>
    /// <value>
    ///   <c>true</c> if board is overclocked; otherwise, <c>false</c>.
    /// </value>
    public bool IsOverclocked
    {
        get
        {
            return _revisionCode.IsOverclocked;
        }
    }

    /// <summary>
    /// Gets a value indicating whether warranty bit 24 is set in the revision code.
    /// </summary>
    public bool IsWarrantyBit24Set => _revisionCode.IsWarrantyBit24Set;

    /// <summary>
    /// Gets a value indicating whether warranty bit 25 is set in the revision code.
    /// </summary>
    public bool IsWarrantyBit25Set => _revisionCode.IsWarrantyBit25Set;

    #endregion

    #region Private Helpers

    /// <summary>
    /// Detect the board CPU information from /proc/cpuinfo
    /// </summary>
    /// <returns>
    /// The <see cref="RaspberryBoardInfo"/>.
    /// </returns>
    internal static RaspberryBoardInfo LoadBoardInfo()
    {
        try
        {
            const string filePath = "/proc/cpuinfo";

            var cpuInfo = File.ReadAllLines(filePath);
            var settings = new Dictionary<string, string>();
            var suffix = string.Empty;

            foreach (var line in cpuInfo)
            {
                var separator = line.IndexOf(':');

                if (!string.IsNullOrWhiteSpace(line) && separator > 0)
                {
                    var key = line.Substring(0, separator).Trim();
                    var val = line.Substring(separator + 1).Trim();
                    if (string.Equals(key, "processor", StringComparison.InvariantCultureIgnoreCase))
                    {
                        suffix = "." + val;
                    }

                    settings.Add(key + suffix, val);
                }
                else
                {
                    suffix = string.Empty;
                }
            }

            return new RaspberryBoardInfo(settings);
        }
        catch
        {
            return new RaspberryBoardInfo(new Dictionary<string, string>());
        }
    }
    #endregion
}
