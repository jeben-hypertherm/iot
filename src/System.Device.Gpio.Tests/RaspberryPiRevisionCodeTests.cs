// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace System.Device.Gpio.Tests;

public class RaspberryPiRevisionCodeTests
{
    [Theory]
    [InlineData(0x0002, false, 0x02, RaspberryBoardInfo.Model.RaspberryPiBRev1)]
    [InlineData(0x0015, false, 0x15, RaspberryBoardInfo.Model.RaspberryPiAPlus)]
    [InlineData(0x00C1, false, 0xC1, RaspberryBoardInfo.Model.RaspberryPiZeroW)]
    public void OldStyleCodesAreMappedFromLegacyRevision(int firmware, bool expectedIsNewStyle, int expectedLegacyRevision, RaspberryBoardInfo.Model expectedModel)
    {
        RaspberryPiRevisionCode revision = RaspberryPiRevisionCode.Create(firmware);

        Assert.Equal(expectedIsNewStyle, revision.IsNewStyle);
        Assert.Equal(expectedLegacyRevision, revision.LegacyBoardRevision);
        Assert.Equal(expectedModel, revision.GetBoardModel());
    }

    [Theory]
    [InlineData(0x800000 | (0x00 << 4), 0x00, RaspberryBoardInfo.Model.RaspberryPiA)]
    [InlineData(0x800000 | (0x01 << 4), 0x01, RaspberryBoardInfo.Model.RaspberryPiBRev1)]
    [InlineData(0x800000 | (0x02 << 4), 0x02, RaspberryBoardInfo.Model.RaspberryPiAPlus)]
    [InlineData(0x800000 | (0x03 << 4), 0x03, RaspberryBoardInfo.Model.RaspberryPiBPlus)]
    [InlineData(0x800000 | (0x04 << 4), 0x04, RaspberryBoardInfo.Model.RaspberryPi2B)]
    [InlineData(0x800000 | (0x05 << 4), 0x05, RaspberryBoardInfo.Model.RaspberryPiAlpha)]
    [InlineData(0x800000 | (0x06 << 4), 0x06, RaspberryBoardInfo.Model.RaspberryPiComputeModule)]
    [InlineData(0x800000 | (0x08 << 4), 0x08, RaspberryBoardInfo.Model.RaspberryPi3B)]
    [InlineData(0x800000 | (0x09 << 4), 0x09, RaspberryBoardInfo.Model.RaspberryPiZero)]
    [InlineData(0x800000 | (0x0A << 4), 0x0A, RaspberryBoardInfo.Model.RaspberryPiComputeModule3)]
    [InlineData(0x800000 | (0x0C << 4), 0x0C, RaspberryBoardInfo.Model.RaspberryPiZeroW)]
    [InlineData(0x800000 | (0x0D << 4), 0x0D, RaspberryBoardInfo.Model.RaspberryPi3BPlus)]
    [InlineData(0x800000 | (0x0E << 4), 0x0E, RaspberryBoardInfo.Model.RaspberryPi3APlus)]
    [InlineData(0x800000 | (0x10 << 4), 0x10, RaspberryBoardInfo.Model.RaspberryPiComputeModule3Plus)]
    [InlineData(0x800000 | (0x11 << 4), 0x11, RaspberryBoardInfo.Model.RaspberryPi4)]
    [InlineData(0x800000 | (0x12 << 4), 0x12, RaspberryBoardInfo.Model.RaspberryPi400)]
    [InlineData(0x800000 | (0x13 << 4), 0x13, RaspberryBoardInfo.Model.RaspberryPiComputeModule4)]
    [InlineData(0x800000 | (0x14 << 4), 0x14, RaspberryBoardInfo.Model.RaspberryPiZero2W)]
    [InlineData(0x800000 | (0x15 << 4), 0x15, RaspberryBoardInfo.Model.RaspberryPi500)]
    [InlineData(0x800000 | (0x17 << 4), 0x17, RaspberryBoardInfo.Model.RaspberryPi5)]
    [InlineData(0x800000 | (0x18 << 4), 0x18, RaspberryBoardInfo.Model.RaspberryPiComputeModule5)]
    [InlineData(0x800000 | (0x1A << 4), 0x1A, RaspberryBoardInfo.Model.RaspberryPiComputeModule5Lite)]
    public void NewStyleCodesSupportAllKnownBoardTypes(int firmware, int expectedType, RaspberryBoardInfo.Model expectedModel)
    {
        RaspberryPiRevisionCode revision = RaspberryPiRevisionCode.Create(firmware);

        Assert.True(revision.IsNewStyle);
        Assert.Equal(expectedType, revision.NewStyleBoardType);
        Assert.Equal(expectedModel, revision.GetBoardModel());
    }

    [Fact]
    public void DecodedButUnmappedNewStyleBoardTypeReturnsUnknown()
    {
        RaspberryPiRevisionCode revision = RaspberryPiRevisionCode.Create(0x800000 | (0x16 << 4));

        Assert.True(revision.IsNewStyle);
        Assert.Equal(0x16, revision.NewStyleBoardType);
        Assert.Equal(RaspberryBoardInfo.Model.Unknown, revision.GetBoardModel());
    }
}
