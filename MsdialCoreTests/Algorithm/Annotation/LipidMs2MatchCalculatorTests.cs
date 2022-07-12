﻿using CompMs.Common.Components;
using CompMs.Common.Parameter;
using CompMs.Common.Parser;
using CompMs.MsdialCore.DataObj;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace CompMs.MsdialCore.Algorithm.Annotation.Tests
{
    [TestClass()]
    public class LipidMs2MatchCalculatorTests
    {
        [TestMethod()]
        public void CalculateTest() {
            var target = new ChromatogramPeakFeature {
                PrecursorMz = 810.604, CollisionCrossSection = 102,
                Spectrum = new List<SpectrumPeak>
                {
                    new SpectrumPeak { Mass = 86.094f, Intensity = 5, },
                    new SpectrumPeak { Mass = 184.073f, Intensity = 100, },
                    new SpectrumPeak { Mass = 524.367f, Intensity = 1, },
                    new SpectrumPeak { Mass = 810.604f, Intensity = 25, },
                }
            };
            var parameter = new MsRefSearchParameterBase
            {
                Ms2Tolerance = 0.05f,
                MassRangeBegin = 0,
                MassRangeEnd = 2000,
                WeightedDotProductCutOff = 0.5f,
                SimpleDotProductCutOff = 0.5f,
                ReverseDotProductCutOff = 0.5f,
                MatchedPeaksPercentageCutOff = 0.5f,
                MinimumSpectrumMatch = 3,
            };
            var reference = new MoleculeMsReference {
                Name = "PC 18:0_20:4", CompoundClass = "PC",
                PrecursorMz = 810.601, CollisionCrossSection = 100,
                AdductType = AdductIonParser.GetAdductIonBean("[M+H]+"),
                Spectrum = new List<SpectrumPeak>
                {
                    new SpectrumPeak { Mass = 184.073f, Intensity = 100 },
                    new SpectrumPeak { Mass = 506.361f, Intensity = 5 },
                    new SpectrumPeak { Mass = 524.372f, Intensity = 5 },
                    new SpectrumPeak { Mass = 526.330f, Intensity = 5 },
                    new SpectrumPeak { Mass = 544.340f, Intensity = 5 },
                    new SpectrumPeak { Mass = 810.601f, Intensity = 30 },
                }
            };

            var calculator = new LipidMs2MatchCalculator();
            var result = calculator.Calculate(new MSScanMatchQuery(target, parameter), reference);

            Assert.AreEqual(0.8547842944, result.WeightedDotProduct, 0.00001);
            Assert.AreEqual(0.8550592192, result.SimpleDotProduct, 0.00001);
            Assert.AreEqual(0.7361639372, result.ReverseDotProduct, 0.00001);
            Assert.AreEqual(3f/6 + 0.5, result.MatchedPeaksPercentage);
            Assert.AreEqual(6, result.MatchedPeaksCount);
            var expected = ((result.WeightedDotProduct + result.SimpleDotProduct + result.ReverseDotProduct) / 3 + result.MatchedPeaksPercentage) / 2; 
            Assert.AreEqual(expected, result.TotalScore);
            Assert.IsTrue(result.IsSpectrumMatch);
        }

        [TestMethod()]
        public void CalculateFailedTest() {
            var target = new ChromatogramPeakFeature {
                PrecursorMz = 810.604, CollisionCrossSection = 102,
                Spectrum = new List<SpectrumPeak>
                {
                    new SpectrumPeak { Mass = 86.094f, Intensity = 5, },
                    new SpectrumPeak { Mass = 184.073f, Intensity = 100, },
                    new SpectrumPeak { Mass = 524.367f, Intensity = 1, },
                    new SpectrumPeak { Mass = 810.604f, Intensity = 25, },
                }
            };
            var parameter = new MsRefSearchParameterBase
            {
                Ms2Tolerance = 0.05f,
                MassRangeBegin = 0,
                MassRangeEnd = 2000,
                WeightedDotProductCutOff = 0.5f,
                SimpleDotProductCutOff = 0.5f,
                ReverseDotProductCutOff = 0.5f,
                MatchedPeaksPercentageCutOff = 0.5f,
                MinimumSpectrumMatch = 3,
            };
            var reference = new MoleculeMsReference {
                Name = "PC 18:0_20:4", CompoundClass = "PC",
                PrecursorMz = 810.601, CollisionCrossSection = 100,
                AdductType = AdductIonParser.GetAdductIonBean("[M+H]+"),
                Spectrum = new List<SpectrumPeak>
                {
                    new SpectrumPeak { Mass = 184.073f, Intensity = 100 },
                    new SpectrumPeak { Mass = 506.361f, Intensity = 5 },
                    new SpectrumPeak { Mass = 524.372f, Intensity = 5 },
                    new SpectrumPeak { Mass = 526.330f, Intensity = 5 },
                    new SpectrumPeak { Mass = 544.340f, Intensity = 5 },
                    new SpectrumPeak { Mass = 810.601f, Intensity = 30 },
                }
            };

            var calculator = new LipidMs2MatchCalculator();

            var result = calculator.Calculate(new MSScanMatchQuery(target, parameter), reference);
            Assert.IsTrue(result.IsSpectrumMatch);

            parameter.WeightedDotProductCutOff = 0.9f;
            result = calculator.Calculate(new MSScanMatchQuery(target, parameter), reference);
            Assert.IsFalse(result.IsSpectrumMatch);

            parameter.WeightedDotProductCutOff = 0.5f;
            parameter.SimpleDotProductCutOff = 0.9f;
            result = calculator.Calculate(new MSScanMatchQuery(target, parameter), reference);
            Assert.IsFalse(result.IsSpectrumMatch);

            parameter.SimpleDotProductCutOff = 0.5f;
            parameter.ReverseDotProductCutOff = 0.8f;
            result = calculator.Calculate(new MSScanMatchQuery(target, parameter), reference);
            Assert.IsFalse(result.IsSpectrumMatch);

            parameter.ReverseDotProductCutOff = 0.5f;
            parameter.MatchedPeaksPercentageCutOff = 0.6f;
            result = calculator.Calculate(new MSScanMatchQuery(target, parameter), reference);
            Assert.IsTrue(result.IsSpectrumMatch);

            parameter.MatchedPeaksPercentageCutOff = 0.5f;
            parameter.MinimumSpectrumMatch = 7;
            result = calculator.Calculate(new MSScanMatchQuery(target, parameter), reference);
            Assert.IsFalse(result.IsSpectrumMatch);
        }
    }
}