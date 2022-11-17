﻿using CompMs.App.Msdial.ExternalApp;
using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.Core;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Information;
using CompMs.App.Msdial.Model.Loader;
using CompMs.App.Msdial.Model.Search;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.CommonMVVM.ChemView;
using CompMs.Graphics.Base;
using CompMs.Graphics.Design;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialCore.Utility;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CompMs.App.Msdial.Model.Lcms
{
    internal sealed class LcmsAnalysisModel : AnalysisModelBase {
        private readonly IDataProvider _provider;

        public LcmsAnalysisModel(
            AnalysisFileBeanModel analysisFileModel,
            IDataProvider provider,
            DataBaseStorage databases,
            DataBaseMapper mapper,
            IMatchResultEvaluator<MsScanMatchResult> evaluator,
            ParameterBase parameter,
            PeakFilterModel peakFilterModel)
            : base(analysisFileModel) {
            if (provider is null) {
                throw new ArgumentNullException(nameof(provider));
            }

            if (mapper is null) {
                throw new ArgumentNullException(nameof(mapper));
            }

            if (evaluator is null) {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (parameter is null) {
                throw new ArgumentNullException(nameof(parameter));
            }

            _provider = provider;
            DataBaseMapper = mapper;
            Parameter = parameter;
            CompoundSearchers = CompoundSearcherCollection.BuildSearchers(databases, DataBaseMapper, parameter.PeakPickBaseParam).Items;

            if (parameter.TargetOmics == TargetOmics.Proteomics) {
                // These 3 lines must be moved to somewhere for swithcing/updating the alignment result
                var proteinResultContainer = MsdialProteomicsSerializer.LoadProteinResultContainer(analysisFileModel.ProteinAssembledResultFilePath);
                var proteinResultContainerModel = new ProteinResultContainerModel(proteinResultContainer, Ms1Peaks, Target);
                ProteinResultContainerModel = proteinResultContainerModel;
            }

            PeakSpotNavigatorModel = new PeakSpotNavigatorModel(Ms1Peaks, peakFilterModel, evaluator, status: ~FilterEnableStatus.Dt).AddTo(Disposables);

            // Peak scatter plot
            var ontologyBrush = new BrushMapData<ChromatogramPeakFeatureModel>(
                    new KeyBrushMapper<ChromatogramPeakFeatureModel, string>(
                        ChemOntologyColor.Ontology2RgbaBrush,
                        peak => peak?.Ontology ?? string.Empty,
                        Color.FromArgb(180, 181, 181, 181)),
                    "Ontology");
            var intensityBrush = new BrushMapData<ChromatogramPeakFeatureModel>(
                    new DelegateBrushMapper<ChromatogramPeakFeatureModel>(
                        peak => Color.FromArgb(
                            180,
                            (byte)(255 * peak.InnerModel.PeakShape.AmplitudeScoreValue),
                            (byte)(255 * (1 - Math.Abs(peak.InnerModel.PeakShape.AmplitudeScoreValue - 0.5))),
                            (byte)(255 - 255 * peak.InnerModel.PeakShape.AmplitudeScoreValue)),
                        enableCache: true),
                    "Intensity");
            var brushes = new[] { intensityBrush, ontologyBrush, };
            BrushMapData<ChromatogramPeakFeatureModel> selectedBrush;
            switch (Parameter.TargetOmics) {
                case TargetOmics.Lipidomics:
                    selectedBrush = ontologyBrush;
                    break;
                case TargetOmics.Metabolomics:
                case TargetOmics.Proteomics:
                default:
                    selectedBrush = intensityBrush;
                    break;
            }
            Brush = selectedBrush.Mapper;
            var labelSource = PeakSpotNavigatorModel.ObserveProperty(m => m.SelectedAnnotationLabel).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            PlotModel = new AnalysisPeakPlotModel(Ms1Peaks, peak => peak.ChromXValue ?? 0, peak => peak.Mass, Target, labelSource, selectedBrush, brushes)
            {
                HorizontalTitle = "Retention time [min]",
                VerticalTitle = "m/z",
                HorizontalProperty = nameof(ChromatogramPeakFeatureModel.ChromXValue),
                VerticalProperty = nameof(ChromatogramPeakFeatureModel.Mass),
            }.AddTo(Disposables);
            Target.Select(
                t =>  $"File: {analysisFileModel.AnalysisFileName}" +
                    (t is null
                        ? string.Empty
                        : $" Spot ID: {t.MasterPeakID} Scan: {t.MS1RawSpectrumIdTop} Mass m/z: {t.Mass:N5}"))
                .Subscribe(title => PlotModel.GraphTitle = title)
                .AddTo(Disposables);

            // Eic chart
            var eicLoader = EicLoader.BuildForAllRange(this._provider, Parameter, ChromXType.RT, ChromXUnit.Min, Parameter.RetentionTimeBegin, Parameter.RetentionTimeEnd);
            EicLoader = EicLoader.BuildForPeakRange(this._provider, Parameter, ChromXType.RT, ChromXUnit.Min, Parameter.RetentionTimeBegin, Parameter.RetentionTimeEnd);
            EicModel = new EicModel(Target, eicLoader) {
                HorizontalTitle = PlotModel.HorizontalTitle,
                VerticalTitle = "Abundance",
            }.AddTo(Disposables);

            ExperimentSpectrumModel = EicModel.Chromatogram
                .Select(chromatogram => chromatogram.ConvertToDisplayChromatogram())
                .Select(chromatogram => new ChromatogramsModel("Experiment chromatogram", chromatogram))
                .DisposePreviousValue()
                .Select(chromatogram => new RangeSelectableChromatogramModel(chromatogram))
                .DisposePreviousValue()
                .CombineLatest(
                    Target.Where(t => t != null),
                    (model, t) => new ExperimentSpectrumModel(model, AnalysisFileModel, provider, t.InnerModel, DataBaseMapper, Parameter))
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            var rawSpectrumLoader = new MultiMsRawSpectrumLoader(provider, parameter).AddTo(Disposables);
            _rawSpectrumLoader = rawSpectrumLoader;
            var decSpectrumLoader = new MsDecSpectrumLoader(decLoader, Ms1Peaks);

            // Ms2 spectrum
            var upperSpecBrush = new KeyBrushMapper<SpectrumComment, string>(
               Parameter.ProjectParam.SpectrumCommentToColorBytes
               .ToDictionary(
                   kvp => kvp.Key,
                   kvp => Color.FromRgb(kvp.Value[0], kvp.Value[1], kvp.Value[2])
               ),
               item => item.ToString(),
               Colors.Blue);
            Color mapToColor(SpectrumComment comment) {
                var commentString = comment.ToString();
                if (Parameter.ProjectParam.SpectrumCommentToColorBytes.TryGetValue(commentString, out var color)) {
                    return Color.FromRgb(color[0], color[1], color[2]);
                }
                else if ((comment & SpectrumComment.doublebond) == SpectrumComment.doublebond
                    && Parameter.ProjectParam.SpectrumCommentToColorBytes.TryGetValue(SpectrumComment.doublebond.ToString(), out color)) {
                    return Color.FromRgb(color[0], color[1], color[2]);
                }
                else {
                    return Colors.Red;
                }
            }
            var lowerSpecBrush = new DelegateBrushMapper<SpectrumComment>(mapToColor, true);
            var spectraExporter = new NistSpectraExporter(Target.Select(t => t?.InnerModel), mapper, Parameter).AddTo(Disposables);
            Ms2SpectrumModel = new RawDecSpectrumsModel(
                Target,
                rawSpectrumLoader,
                decSpectrumLoader,
                new MsRefSpectrumLoader(mapper),
                new PropertySelector<SpectrumPeak, double>(peak => peak.Mass),
                new PropertySelector<SpectrumPeak, double>(peak => peak.Intensity),
                new GraphLabels("Measure vs. Reference", "m/z", "Relative abundance", nameof(SpectrumPeak.Mass), nameof(SpectrumPeak.Intensity)),
                nameof(SpectrumPeak.SpectrumComment),
                Observable.Return(upperSpecBrush),
                Observable.Return(lowerSpecBrush),
                Observable.Return(spectraExporter),
                Observable.Return(spectraExporter),
                Observable.Return((ISpectraExporter)null)).AddTo(Disposables);
            

            // Raw vs Purified spectrum model
            RawPurifiedSpectrumsModel = new RawPurifiedSpectrumsModel(
                Target,
                rawSpectrumLoader,
                decSpectrumLoader,
                peak => peak.Mass,
                peak => peak.Intensity) {
                GraphTitle = "Raw vs. Purified spectrum",
                HorizontalTitle = "m/z",
                VerticalTitle = "Absolute abundance",
                HorizontalProperty = nameof(SpectrumPeak.Mass),
                VerticalProperty = nameof(SpectrumPeak.Intensity),
                LabelProperty = nameof(SpectrumPeak.Mass),
                OrderingProperty = nameof(SpectrumPeak.Intensity),
            }.AddTo(Disposables);

            // Ms2 chromatogram
            Ms2ChromatogramsModel = new Ms2ChromatogramsModel(Target, MsdecResult, rawSpectrumLoader, provider, Parameter).AddTo(Disposables);

            // SurveyScan
            var msdataType = Parameter.MSDataType;
            var surveyScanSpectrum = new SurveyScanSpectrum(Target, t =>
            {
                if (t is null) {
                    return Observable.Return(new List<SpectrumPeakWrapper>());
                }
                return Observable.FromAsync(provider.LoadMsSpectrumsAsync)
                    .Select(spectrums =>
                        {
                            var spectra = DataAccess.GetCentroidMassSpectra(
                                spectrums[t.MS1RawSpectrumIdTop],
                                msdataType, 0, float.MinValue, float.MaxValue);
                            return spectra.Select(peak => new SpectrumPeakWrapper(peak)).ToList();
                        });
            }).AddTo(Disposables);
            SurveyScanModel = new SurveyScanModel(
                surveyScanSpectrum,
                spec => spec.Mass,
                spec => spec.Intensity).AddTo(Disposables);
            SurveyScanModel.Elements.VerticalTitle = "Abundance";
            SurveyScanModel.Elements.HorizontalProperty = nameof(SpectrumPeakWrapper.Mass);
            SurveyScanModel.Elements.VerticalProperty = nameof(SpectrumPeakWrapper.Intensity);

            // Peak table
            PeakTableModel = new LcmsAnalysisPeakTableModel(Ms1Peaks, Target).AddTo(Disposables);

            var rtSpotFocus = new ChromSpotFocus(PlotModel.HorizontalAxis, RtTol, Target.Select(t => t?.ChromXValue ?? 0d), "F2", "RT(min)", isItalic: false).AddTo(Disposables);
            var mzSpotFocus = new ChromSpotFocus(PlotModel.VerticalAxis, MzTol, Target.Select(t => t?.Mass ?? 0d), "F3", "m/z", isItalic: true).AddTo(Disposables);
            var idSpotFocus = new IdSpotFocus<ChromatogramPeakFeatureModel>(
                Target,
                id => Ms1Peaks.Argmin(p => Math.Abs(p.MasterPeakID - id)),
                Target.Select(t => t?.MasterPeakID ?? 0d),
                "Region focus by ID",
                (rtSpotFocus, peak => peak.ChromXValue ?? 0d),
                (mzSpotFocus, peak => peak.Mass)).AddTo(Disposables);
            FocusNavigatorModel = new FocusNavigatorModel(idSpotFocus, rtSpotFocus, mzSpotFocus);

            CanSaveRawSpectra = Target.Select(t => t?.InnerModel != null)
                .ToReadOnlyReactivePropertySlim(initialValue: false)
                .AddTo(Disposables);

            var peakInformationModel = new PeakInformationAnalysisModel(Target).AddTo(Disposables);
            peakInformationModel.Add(
                t => new RtPoint(t?.InnerModel.ChromXs.RT.Value ?? 0d, t.Refer<MoleculeMsReference>(mapper)?.ChromXs.RT.Value),
                t => new MzPoint(t?.Mass ?? 0d, t.Refer<MoleculeMsReference>(mapper)?.PrecursorMz));
            peakInformationModel.Add(
                t => new HeightAmount(t?.Intensity ?? 0d),
                t => new AreaAmount(t?.PeakArea ?? 0d));
            PeakInformationModel = peakInformationModel;
            var compoundDetailModel = new CompoundDetailModel(Target.Select(t => t?.ScanMatchResult), mapper).AddTo(Disposables);
            compoundDetailModel.Add(
                r_ => new MzSimilarity(r_?.AcurateMassSimilarity ?? 0d),
                r_ => new RtSimilarity(r_?.RtSimilarity ?? 0d),
                r_ => new SpectrumSimilarity(r_?.WeightedDotProduct ?? 0d, r_?.ReverseDotProduct ?? 0d));
            CompoundDetailModel = compoundDetailModel;
        }

        private static readonly double RtTol = 0.5;
        private static readonly double MzTol = 20;

        public DataBaseMapper DataBaseMapper { get; }
        public ParameterBase Parameter { get; }

        public IReadOnlyList<CompoundSearcher> CompoundSearchers { get; }

        public EicLoader EicLoader { get; }

        public AnalysisPeakPlotModel PlotModel { get; }

        public EicModel EicModel { get; }
        public ReadOnlyReactivePropertySlim<ExperimentSpectrumModel> ExperimentSpectrumModel { get; }

        private readonly IMsSpectrumLoader<ChromatogramPeakFeatureModel> _rawSpectrumLoader;

        public RawDecSpectrumsModel Ms2SpectrumModel { get; }
        public RawPurifiedSpectrumsModel RawPurifiedSpectrumsModel { get; }
        public Ms2ChromatogramsModel Ms2ChromatogramsModel { get; }
        public SurveyScanModel SurveyScanModel { get; }

        public LcmsAnalysisPeakTableModel PeakTableModel { get; }

        public IBrushMapper<ChromatogramPeakFeatureModel> Brush { get; }

        public FocusNavigatorModel FocusNavigatorModel { get; }

        public PeakSpotNavigatorModel PeakSpotNavigatorModel { get; }

        public ICompoundSearchModel CreateCompoundSearchModel() {
            if (Target.Value?.InnerModel is null || MsdecResult.Value is null) {
                return null;
            }

            return new LcmsCompoundSearchModel(AnalysisFileModel, Target.Value, MsdecResult.Value, CompoundSearchers);
        }

        public override void SearchFragment() {
            MsdialCore.Algorithm.FragmentSearcher.Search(Ms1Peaks.Select(n => n.InnerModel).ToList(), decLoader, Parameter);
        }

        public void SaveSpectra(string filename) {
            using (var file = File.Open(filename, FileMode.Create)) {
                SpectraExport.SaveSpectraTable(
                    (ExportSpectraFileFormat)Enum.Parse(typeof(ExportSpectraFileFormat), Path.GetExtension(filename).Trim('.')),
                    file,
                    Target.Value.InnerModel,
                    MsdecResult.Value,
                    _provider.LoadMs1Spectrums(),
                    DataBaseMapper,
                    Parameter);
            }
        }

        public bool CanSaveSpectra() => Target.Value.InnerModel != null && MsdecResult.Value != null;

        public async Task SaveRawSpectra(string filename) {
            using (var file = File.Open(filename, FileMode.Create)) {
                var target = Target.Value;
                var spectrum = await _rawSpectrumLoader.LoadSpectrumAsObservable(target).FirstAsync();
                SpectraExport.SaveSpectraTable(
                    (ExportSpectraFileFormat)Enum.Parse(typeof(ExportSpectraFileFormat), Path.GetExtension(filename).Trim('.')),
                    file,
                    target.InnerModel,
                    new MSScanProperty() { Spectrum = spectrum },
                    _provider.LoadMs1Spectrums(),
                    DataBaseMapper,
                    Parameter);
            }
        }

        public ReadOnlyReactivePropertySlim<bool> CanSaveRawSpectra { get; }
        public PeakInformationAnalysisModel PeakInformationModel { get; }
        public CompoundDetailModel CompoundDetailModel { get; }
        public ProteinResultContainerModel ProteinResultContainerModel { get; }

        public void GoToMsfinderMethod() {
            if (Target.Value is null || MsdecResult.Value is null) {
                return;
            }
            MsDialToExternalApps.SendToMsFinderProgram(
                AnalysisFileModel,
                Target.Value.InnerModel,
                MsdecResult.Value,
                _provider.LoadMs1Spectrums(),
                DataBaseMapper,
                Parameter);
        }
    }
}
