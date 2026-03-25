// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Device.Gpio.Drivers;
using Xunit;
using Xunit.Abstractions;

namespace System.Device.Gpio.Tests;

[Trait("requirement", "root")]
[Trait("feature", "gpio")]
[Trait("feature", "gpio-rpi5")]
public class RaspberryPi5DriverTests : GpioControllerTestBase
{
    public RaspberryPi5DriverTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    protected override GpioDriver GetTestDriver() => new RaspberryPi5Driver();

    [Fact]
    public void DriverReportsRp1ChipInfo()
    {
        using var driver = new RaspberryPi5Driver();
        GpioChipInfo chipInfo = driver.GetChipInfo();
        Assert.Equal(54, chipInfo.NumLines);
        Assert.Equal("RP1", chipInfo.Label);
    }
}
