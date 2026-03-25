// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Device.Gpio;

internal sealed class RaspberryPiRevisionCode
{
    private const int NewStyleRevisionFlag = 1 << 23;

    private static readonly Dictionary<int, RaspberryBoardInfo.Model> s_oldStyleModels = new()
    {
        { 0x2, RaspberryBoardInfo.Model.RaspberryPiBRev1 },
        { 0x3, RaspberryBoardInfo.Model.RaspberryPiBRev1 },
        { 0x4, RaspberryBoardInfo.Model.RaspberryPiBRev2 },
        { 0x5, RaspberryBoardInfo.Model.RaspberryPiBRev2 },
        { 0x6, RaspberryBoardInfo.Model.RaspberryPiBRev2 },
        { 0xd, RaspberryBoardInfo.Model.RaspberryPiBRev2 },
        { 0xe, RaspberryBoardInfo.Model.RaspberryPiBRev2 },
        { 0xf, RaspberryBoardInfo.Model.RaspberryPiBRev2 },
        { 0x7, RaspberryBoardInfo.Model.RaspberryPiA },
        { 0x8, RaspberryBoardInfo.Model.RaspberryPiA },
        { 0x9, RaspberryBoardInfo.Model.RaspberryPiA },
        { 0x10, RaspberryBoardInfo.Model.RaspberryPiBPlus },
        { 0x13, RaspberryBoardInfo.Model.RaspberryPiBPlus },
        { 0x32, RaspberryBoardInfo.Model.RaspberryPiBPlus },
        { 0x11, RaspberryBoardInfo.Model.RaspberryPiComputeModule },
        { 0x14, RaspberryBoardInfo.Model.RaspberryPiComputeModule },
        { 0x61, RaspberryBoardInfo.Model.RaspberryPiComputeModule },
        { 0x12, RaspberryBoardInfo.Model.RaspberryPiAPlus },
        { 0x15, RaspberryBoardInfo.Model.RaspberryPiAPlus },
        { 0x21, RaspberryBoardInfo.Model.RaspberryPiAPlus },
        { 0x1040, RaspberryBoardInfo.Model.RaspberryPi2B },
        { 0x1041, RaspberryBoardInfo.Model.RaspberryPi2B },
        { 0x2042, RaspberryBoardInfo.Model.RaspberryPi2B },
        { 0x92, RaspberryBoardInfo.Model.RaspberryPiZero },
        { 0x93, RaspberryBoardInfo.Model.RaspberryPiZero },
        { 0xC1, RaspberryBoardInfo.Model.RaspberryPiZeroW },
        { 0x2120, RaspberryBoardInfo.Model.RaspberryPiZero2W },
        { 0x2082, RaspberryBoardInfo.Model.RaspberryPi3B },
        { 0x2083, RaspberryBoardInfo.Model.RaspberryPi3B },
        { 0x20D3, RaspberryBoardInfo.Model.RaspberryPi3BPlus },
        { 0x20D4, RaspberryBoardInfo.Model.RaspberryPi3BPlus },
        { 0x20E0, RaspberryBoardInfo.Model.RaspberryPi3APlus },
        { 0x20E1, RaspberryBoardInfo.Model.RaspberryPi3APlus },
        { 0x20A0, RaspberryBoardInfo.Model.RaspberryPiComputeModule3 },
        { 0x2100, RaspberryBoardInfo.Model.RaspberryPiComputeModule3 },
        { 0x3111, RaspberryBoardInfo.Model.RaspberryPi4 },
        { 0x3112, RaspberryBoardInfo.Model.RaspberryPi4 },
        { 0x3114, RaspberryBoardInfo.Model.RaspberryPi4 },
        { 0x3115, RaspberryBoardInfo.Model.RaspberryPi4 },
        { 0x3140, RaspberryBoardInfo.Model.RaspberryPiComputeModule4 },
        { 0x3141, RaspberryBoardInfo.Model.RaspberryPiComputeModule4 },
        { 0x3130, RaspberryBoardInfo.Model.RaspberryPi400 },
        { 0x3131, RaspberryBoardInfo.Model.RaspberryPi400 },
        { 0x4170, RaspberryBoardInfo.Model.RaspberryPi5 },
        { 0x4180, RaspberryBoardInfo.Model.RaspberryPiComputeModule5 },
        { 0x41A0, RaspberryBoardInfo.Model.RaspberryPiComputeModule5Lite },
    };

    private static readonly Dictionary<int, RaspberryBoardInfo.Model> s_newStyleModels = new()
    {
        { 0x00, RaspberryBoardInfo.Model.RaspberryPiA },
        { 0x01, RaspberryBoardInfo.Model.RaspberryPiBRev1 },
        { 0x02, RaspberryBoardInfo.Model.RaspberryPiAPlus },
        { 0x03, RaspberryBoardInfo.Model.RaspberryPiBPlus },
        { 0x04, RaspberryBoardInfo.Model.RaspberryPi2B },
        { 0x05, RaspberryBoardInfo.Model.RaspberryPiAlpha },
        { 0x06, RaspberryBoardInfo.Model.RaspberryPiComputeModule },
        { 0x08, RaspberryBoardInfo.Model.RaspberryPi3B },
        { 0x09, RaspberryBoardInfo.Model.RaspberryPiZero },
        { 0x0A, RaspberryBoardInfo.Model.RaspberryPiComputeModule3 },
        { 0x0C, RaspberryBoardInfo.Model.RaspberryPiZeroW },
        { 0x0D, RaspberryBoardInfo.Model.RaspberryPi3BPlus },
        { 0x0E, RaspberryBoardInfo.Model.RaspberryPi3APlus },
        { 0x10, RaspberryBoardInfo.Model.RaspberryPiComputeModule3Plus },
        { 0x11, RaspberryBoardInfo.Model.RaspberryPi4 },
        { 0x12, RaspberryBoardInfo.Model.RaspberryPi400 },
        { 0x13, RaspberryBoardInfo.Model.RaspberryPiComputeModule4 },
        { 0x14, RaspberryBoardInfo.Model.RaspberryPiZero2W },
        { 0x15, RaspberryBoardInfo.Model.RaspberryPi500 },
        { 0x17, RaspberryBoardInfo.Model.RaspberryPi5 },
        { 0x18, RaspberryBoardInfo.Model.RaspberryPiComputeModule5 },
        { 0x1A, RaspberryBoardInfo.Model.RaspberryPiComputeModule5Lite },
    };

    private RaspberryPiRevisionCode(int firmware)
    {
        RawRevision = firmware;
    }

    public int RawRevision { get; }

    public bool IsNewStyle => (RawRevision & NewStyleRevisionFlag) != 0;

    public int LegacyBoardRevision => RawRevision & 0xFFFF;

    public int NewStyleBoardType => (RawRevision >> 4) & 0xFF;

    public static RaspberryPiRevisionCode Create(int firmware)
    {
        return new RaspberryPiRevisionCode(firmware);
    }

    public RaspberryBoardInfo.Model GetBoardModel()
    {
        if (IsNewStyle)
        {
            return s_newStyleModels.TryGetValue(NewStyleBoardType, out RaspberryBoardInfo.Model model) ? model : RaspberryBoardInfo.Model.Unknown;
        }

        return s_oldStyleModels.TryGetValue(LegacyBoardRevision, out RaspberryBoardInfo.Model legacyModel) ? legacyModel : RaspberryBoardInfo.Model.Unknown;
    }
}
