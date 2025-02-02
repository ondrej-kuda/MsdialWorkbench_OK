﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Loader;
using CompMs.Common.Components;
using CompMs.Common.Extension;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialLcImMsApi.Parameter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CompMs.App.Msdial.Model.Lcimms
{
    internal sealed class LcimmsEicLoader : IChromatogramLoader<ChromatogramPeakFeatureModel>
    {
        private readonly MsdialLcImMsParameter _parameter;
        private readonly RawSpectra _rawSpectra;

        public LcimmsEicLoader(IDataProvider provider, MsdialLcImMsParameter parameter, RawSpectra rawSpectra) {
            if (provider is null) {
                throw new ArgumentNullException(nameof(provider));
            }

            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            _rawSpectra = rawSpectra ?? throw new ArgumentNullException(nameof(rawSpectra));
        }

        public Task<PeakChromatogram> LoadChromatogramAsync(ChromatogramPeakFeatureModel? target, CancellationToken token) {
            if (target is null) {
                return Task.FromResult(new PeakChromatogram(new List<PeakItem>(0), new List<PeakItem>(0), null, string.Empty, Colors.Black, ChromXType.Drift, ChromXUnit.Msec));
            }
            return Task.Run(() =>
            {
                var ms1Peaks = _rawSpectra.GetDriftChromatogramByScanRtMz(target.InnerModel.MS1RawSpectrumIdTop, (float)target.InnerModel.PeakFeature.ChromXsTop.RT.Value, (float)_parameter.AccumulatedRtRange, (float)target.Mass, _parameter.CentroidMs1Tolerance);
                var eic = ms1Peaks.Smoothing(_parameter.SmoothingMethod, _parameter.SmoothingLevel)
                    .Where(peak => peak != null)
                    .Select(peak => new PeakItem(peak))
                    .ToList();
                var area = eic.Where(peak => target.ChromXLeftValue <= peak.Time && peak.Time <= target.ChromXRightValue).ToList();
                var top = area.Argmin(peak => Math.Abs((target.ChromXValue ?? double.MaxValue) - peak.Time));
                return new PeakChromatogram(eic, area, top, string.Empty, Colors.Black, ChromXType.Drift, ChromXUnit.Msec, $"EIC chromatogram of {target.Mass:N4} tolerance [Da]: {_parameter.CentroidMs1Tolerance:F} RT [min]: {target.InnerModel.PeakFeature.ChromXsTop.RT.Value:F2} tolerance [min]: {_parameter.AccumulatedRtRange} Max intensity: {area.Max(peak => peak.Intensity):F0}");
            });
        }
        PeakChromatogram IChromatogramLoader<ChromatogramPeakFeatureModel>.EmptyChromatogram { get; } = new PeakChromatogram(new List<PeakItem>(0), new List<PeakItem>(0), null, string.Empty, Colors.Black, ChromXType.Drift, ChromXUnit.Msec);
    }
}
