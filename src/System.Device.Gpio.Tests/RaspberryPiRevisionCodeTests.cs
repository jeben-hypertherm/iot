// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Xunit;

namespace System.Device.Gpio.Tests;

public class RaspberryPiRevisionCodeTests
{
    private static readonly Type s_revisionCodeType = GetRevisionCodeType();

    [Theory]
    [InlineData("2082", "RaspberryPi3B")]
    [InlineData("20d3", "RaspberryPi3BPlus")]
    [InlineData("20e0", "RaspberryPi3APlus")]
    [InlineData("3111", "RaspberryPi4")]
    [InlineData("3130", "RaspberryPi400")]
    [InlineData("4170", "RaspberryPi5")]
    [InlineData("4180", "RaspberryPiComputeModule5")]
    [InlineData("41a0", "RaspberryPiComputeModule5Lite")]
    [InlineData("c1", "RaspberryPiZeroW")]
    public void TryParse_MapsKnownCodes(string code, string expectedModelName)
    {
        object? revision = ParseRevisionCode(code);

        Assert.NotNull(revision);
        Assert.Equal(expectedModelName, GetProperty(revision, "BoardModel")!.ToString());
    }

    [Theory]
    [InlineData("a02082", "RaspberryPi3B", "Gb1", "SonyUk", "Bcm2837")]
    [InlineData("a020d3", "RaspberryPi3BPlus", "Gb1", "SonyUk", "Bcm2837")]
    [InlineData("c03111", "RaspberryPi4", "Gb4", "SonyUk", "Bcm2711")]
    [InlineData("c04170", "RaspberryPi5", "Gb4", "SonyUk", "Bcm2712")]
    public void TryParse_DecodesNewStyleMetadata(string code, string expectedModel, string expectedMemory, string expectedManufacturer, string expectedProcessor)
    {
        object? revision = ParseRevisionCode(code);

        Assert.NotNull(revision);
        Assert.True((bool)GetProperty(revision, "IsNewStyle")!);
        Assert.Equal(expectedModel, GetProperty(revision, "BoardModel")!.ToString());
        Assert.Equal(expectedMemory, GetProperty(revision, "MemorySize")!.ToString());
        Assert.Equal(expectedManufacturer, GetProperty(revision, "Manufacturer")!.ToString());
        Assert.Equal(expectedProcessor, GetProperty(revision, "Processor")!.ToString());
    }

    [Theory]
    [InlineData("zz")]
    [InlineData("")]
    [InlineData("  ")]
    public void TryParse_RejectsInvalidInput(string code)
    {
        object? revision = ParseRevisionCode(code);

        Assert.Null(revision);
    }

    [Fact]
    public void Parse_DecodesWarrantyBits()
    {
        MethodInfo parseMethod = s_revisionCodeType.GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic)!;
        object revision = parseMethod.Invoke(null, new object[] { 0x03000000 })!;

        Assert.True((bool)GetProperty(revision, "IsWarrantyBit24Set")!);
        Assert.True((bool)GetProperty(revision, "IsWarrantyBit25Set")!);
    }

    private static object? ParseRevisionCode(string code)
    {
        MethodInfo tryParse = s_revisionCodeType.GetMethod("TryParse", BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] args = new object?[] { code, null };
        bool parsed = (bool)tryParse.Invoke(null, args)!;
        return parsed ? args[1] : null;
    }

    private static object? GetProperty(object instance, string propertyName)
    {
        return s_revisionCodeType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(instance);
    }

    private static Type GetRevisionCodeType()
    {
        Type? revisionCodeType = typeof(GpioController).Assembly.GetType("System.Device.Gpio.RaspberryPiRevisionCode", throwOnError: false);
        if (revisionCodeType is null)
        {
            throw new InvalidOperationException("Could not find internal type System.Device.Gpio.RaspberryPiRevisionCode. The parser class may have been moved or renamed.");
        }

        return revisionCodeType;
    }
}
