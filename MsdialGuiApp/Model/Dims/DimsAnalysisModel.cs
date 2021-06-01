﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.Common.Components;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.CommonMVVM;
using CompMs.Graphics.AxisManager.Generic;
using CompMs.Graphics.Core.Base;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Dims
{
    public class DimsAnalysisModel : BindableBase, IDisposable
    {
        public DimsAnalysisModel(
            AnalysisFileBean analysisFile,
            IDataProvider provider,
            IMatchResultRefer refer,
            ParameterBase parameter,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> mspAnnotator, IAnnotator<ChromatogramPeakFeature, MSDecResult> textDBAnnotator) {

            this.provider = provider;
            this.mspAnnotator = mspAnnotator;
            this.textDBAnnotator = textDBAnnotator;

            AnalysisFile = analysisFile;
            FileName = analysisFile.AnalysisFileName;
            Parameter = parameter;

            var peaks = MsdialSerializer.LoadChromatogramPeakFeatures(analysisFile.PeakAreaBeanInformationFilePath);
            ms1Peaks = new ObservableCollection<ChromatogramPeakFeatureModel>(
                peaks.Select(peak => new ChromatogramPeakFeatureModel(peak, parameter.TargetOmics != TargetOmics.Metabolomics)));
            Peaks = peaks;

            HorizontalAxis = new AxisData(new ContinuousAxisManager<double>(MassMin, MassMax), "Mass", "m/z");
            VerticalAxis = new AxisData(new ContinuousAxisManager<double>(-0.5, 0.5), "KMD", "Kendrick mass defect");
            PlotModel = new AnalysisPeakPlotModel(Ms1Peaks, HorizontalAxis.Axis, VerticalAxis.Axis);

            var abundanceAxis = new AxisData(new ContinuousAxisManager<double>(0, 1), "Intensity", "Abundance");
            var massAxis = new AxisData(HorizontalAxis.Axis, "ChromXValue", "m/z");

            EicModel = new EicModel(
                massAxis,
                abundanceAxis,
                new DimsEicLoader(
                    provider,
                    Parameter,
                    ChromXType.Mz,
                    ChromXUnit.Mz,
                    Parameter.MassRangeBegin,
                    Parameter.MassRangeEnd)
            );

            var ms2MeasureIntensityAxis = new AxisData(
                new ContinuousAxisManager<double>(0, 1, 0, 0),
                "Intensity",
                "Abundance");
            var decLoader = new MsDecSpectrumLoader(analysisFile.DeconvolutionFilePath, ms1Peaks).AddTo(disposables);
            Ms2SpectrumModel = new RawDecSpectrumsModel(
                horizontalData: new AxisData(new ContinuousAxisManager<double>(0, 1), "Mass", "m/z"),
                rawVerticalData: ms2MeasureIntensityAxis,
                rawLoader: new MsRawSpectrumLoader(provider, Parameter),
                decVerticalData: ms2MeasureIntensityAxis,
                decLoader: decLoader,
                refVerticalData: new AxisData(new ContinuousAxisManager<double>(0, 1, 0, 0), "Intensity", "Abundance"),
                refLoader: new MsRefSpectrumLoader(refer),
                graphTitle: "Measure vs. Reference");
            

            EicModel.PropertyChanged += OnEicChanged;
        }

        private readonly CompositeDisposable disposables = new CompositeDisposable();
        private readonly IDataProvider provider;

        public AnalysisFileBean AnalysisFile { get; }
        public ParameterBase Parameter { get; }

        public IAnnotator<ChromatogramPeakFeature, MSDecResult> MspAnnotator => mspAnnotator;
        public IAnnotator<ChromatogramPeakFeature, MSDecResult> TextDBAnnotator => textDBAnnotator;

        private readonly IAnnotator<ChromatogramPeakFeature, MSDecResult> mspAnnotator, textDBAnnotator;

        public string FileName {
            get => fileName;
            set => SetProperty(ref fileName, value);
        }
        private string fileName;

        public ObservableCollection<ChromatogramPeakFeatureModel> Ms1Peaks => ms1Peaks;
        private readonly ObservableCollection<ChromatogramPeakFeatureModel> ms1Peaks = new ObservableCollection<ChromatogramPeakFeatureModel>();

        public double MassMin => Ms1Peaks.DefaultIfEmpty().Min(peak => peak.Mass);
        public double MassMax => Ms1Peaks.DefaultIfEmpty().Max(peak => peak.Mass);

        public List<ChromatogramPeakFeature> Peaks { get; } = new List<ChromatogramPeakFeature>();

        public ChromatogramPeakFeatureModel Target {
            get => target;
            set {
                if (SetProperty(ref target, value)) {
                    _ = OnTargetChangedAsync(target);
                }
            }
        }
        private ChromatogramPeakFeatureModel target;

        public AxisData HorizontalAxis {
            get => horizontalAxis;
            set => SetProperty(ref horizontalAxis, value);
        }
        private AxisData horizontalAxis;

        public AxisData VerticalAxis {
            get => verticalAxis;
            set => SetProperty(ref verticalAxis, value);
        }
        private AxisData verticalAxis;

        public AnalysisPeakPlotModel PlotModel {
            get => plotModel;
            private set {
                var newValue = value;
                var oldValue = plotModel;
                if (SetProperty(ref plotModel, value)) {
                    if (oldValue != null) {
                        oldValue.PropertyChanged -= OnPlotModelTargetChanged;
                    }
                    if (newValue != null) {
                        newValue.PropertyChanged += OnPlotModelTargetChanged;
                    }
                }
            }
        }
        private AnalysisPeakPlotModel plotModel;

        private void OnPlotModelTargetChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(PlotModel.Target)) {
                Target = PlotModel.Target;
            }
        }

        public EicModel EicModel { get; }

        private void OnEicChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(EicModel.Eic)) {
                var axis = EicModel.VerticalData;
                var type = typeof(ChromatogramPeakWrapper);
                var prop = type.GetProperty(axis.Property);
                EicModel.VerticalData = new AxisData(
                    ContinuousAxisManager<double>.Build(EicModel.Eic, peak => (double)prop.GetValue(peak), 0, 0),
                    axis.Property,
                    axis.Title);
            }
        }

        public RawDecSpectrumsModel Ms2SpectrumModel { get; }

        private CancellationTokenSource cts;
        public async Task OnTargetChangedAsync(ChromatogramPeakFeatureModel target) {
            cts?.Cancel();
            var localCts = cts = new CancellationTokenSource();

            try {
                await OnTargetChangedAsync(target, localCts.Token).ContinueWith(
                    t => {
                        localCts.Dispose();
                        if (cts == localCts) {
                            cts = null;
                        }
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {

            }
        }

        async Task OnTargetChangedAsync(ChromatogramPeakFeatureModel target, CancellationToken token) {
            await Task.WhenAll(
                EicModel.LoadEicAsync(target, token),
                Ms2SpectrumModel.LoadSpectrumAsync(target, token)
            ).ConfigureAwait(false);
        }

        public string RawSplashKey {
            get => rawSplashKey;
            set => SetProperty(ref rawSplashKey, value);
        }
        private string rawSplashKey = string.Empty;

        public string DeconvolutionSplashKey {
            get => deconvolutionSplashKey;
            set => SetProperty(ref deconvolutionSplashKey, value);
        }
        private string deconvolutionSplashKey = string.Empty;

        public MSDecResult MsdecResult => msdecResult;
        private MSDecResult msdecResult = null;
        private bool disposedValue;
        private static readonly double MzTol = 20;
        public void FocusByMz(IAxisManager axis, double mz) {
            axis?.Focus(mz - MzTol, mz + MzTol);
        }       

        public void FocusById(IAxisManager mzAxis, int id) {
            var focus = Ms1Peaks.FirstOrDefault(peak => peak.InnerModel.MasterPeakID == id);
            Target = focus;
            FocusByMz(mzAxis, focus.Mass);
        }

        public void SaveSpectra(string filename) {
            SpectraExport.SaveSpectraTable(
                (ExportSpectraFileFormat)Enum.Parse(typeof(ExportSpectraFileFormat), Path.GetExtension(filename).Trim('.')),
                filename,
                Target.InnerModel,
                msdecResult,
                Parameter);
        }

        public bool CanSaveSpectra() => Target.InnerModel != null && msdecResult != null;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    disposables.Dispose();                   
                }

                disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
