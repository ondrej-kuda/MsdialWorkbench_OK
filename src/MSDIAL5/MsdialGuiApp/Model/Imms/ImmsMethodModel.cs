﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.Core;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Export;
using CompMs.App.Msdial.Model.Loader;
using CompMs.App.Msdial.Model.Search;
using CompMs.Common.Components;
using CompMs.Common.Enum;
using CompMs.Graphics.UI.ProgressBar;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialImmsCore.Algorithm.Alignment;
using CompMs.MsdialImmsCore.Export;
using CompMs.MsdialImmsCore.Parameter;
using CompMs.MsdialImmsCore.Process;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.Imms
{
    internal sealed class ImmsMethodModel : MethodModelBase
    {
        static ImmsMethodModel() {
            CHROMATOGRAM_SPOT_SERIALIZER = ChromatogramSerializerFactory.CreateSpotSerializer("CSS1", ChromXType.Drift);
        }
        private static readonly ChromatogramSerializer<ChromatogramSpotInfo> CHROMATOGRAM_SPOT_SERIALIZER;

        private readonly IMessageBroker _broker;
        private readonly IMsdialDataStorage<MsdialImmsParameter> _storage;
        private readonly FilePropertiesModel _projectBaseParameter;
        private readonly FacadeMatchResultEvaluator _matchResultEvaluator;
        private readonly PeakSpotFiltering<AlignmentSpotPropertyModel> _peakSpotFiltering;

        public ImmsMethodModel(AnalysisFileBeanModelCollection analysisFileBeanModelCollection, AlignmentFileBeanModelCollection alignmentFileBeanModelCollection, IMsdialDataStorage<MsdialImmsParameter> storage, FilePropertiesModel projectBaseParameter, StudyContextModel studyContext, IMessageBroker broker)
            : base(analysisFileBeanModelCollection, alignmentFileBeanModelCollection, projectBaseParameter) {
            _storage = storage;
            _projectBaseParameter = projectBaseParameter ?? throw new ArgumentNullException(nameof(projectBaseParameter));
            StudyContext = studyContext;
            _broker = broker;
            _matchResultEvaluator = FacadeMatchResultEvaluator.FromDataBases(storage.DataBases);

            var parameter = _storage.Parameter;
            if (parameter.ProviderFactoryParameter is null) {
                parameter.ProviderFactoryParameter = new ImmsAverageDataProviderFactoryParameter(0.01, 0.002, 0, 100);
            }
            ProviderFactory = parameter.ProviderFactoryParameter.Create(5, true);

            PeakFilterModel = new PeakFilterModel(DisplayFilter.All);

            List<AnalysisFileBean> analysisFiles = analysisFileBeanModelCollection.AnalysisFiles.Select(f => f.File).ToList();
            var filterEnabled = FilterEnableStatus.All & ~FilterEnableStatus.Rt & ~FilterEnableStatus.Protein;
            if (parameter.TargetOmics == TargetOmics.Proteomics) {
                filterEnabled |= FilterEnableStatus.Protein;
            }
            _peakSpotFiltering = new PeakSpotFiltering<AlignmentSpotPropertyModel>(filterEnabled).AddTo(Disposables);
            var filter = _peakSpotFiltering.CreateFilter(PeakFilterModel, _matchResultEvaluator.Contramap((AlignmentSpotPropertyModel spot) => spot.ScanMatchResult), filterEnabled);
            var metadataAccessorFactory = new ImmsAlignmentMetadataAccessorFactory(storage.DataBaseMapper, storage.Parameter);
            ReadOnlyReactivePropertySlim<ImmsAlignmentModel?> currentAlignmentResult = this.ObserveProperty(m => m.AlignmentModel).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            AlignmentFilesForExport alignmentFilesForExport = new AlignmentFilesForExport(alignmentFileBeanModelCollection.Files, this.ObserveProperty(m => m.AlignmentFile)).AddTo(Disposables);
            var isNormalized = alignmentFilesForExport.CanExportNormalizedData(currentAlignmentResult.Select(r => r?.NormalizationSetModel.IsNormalized ?? Observable.Return(false)).Switch()).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            AlignmentPeakSpotSupplyer peakSpotSupplyer = new AlignmentPeakSpotSupplyer(currentAlignmentResult, filter);
            var peakGroup = new AlignmentExportGroupModel(
                "Peaks",
                new ExportMethod(
                    analysisFiles,
                    metadataAccessorFactory,
                    ExportFormat.Tsv,
                    ExportFormat.Csv
                ),
                new[]
                {
                    new ExportType("Raw data (Height)", new LegacyQuantValueAccessor("Height", storage.Parameter), "Height", new List<StatsValue> { StatsValue.Average, StatsValue.Stdev }, true),
                    new ExportType("Raw data (Area)", new LegacyQuantValueAccessor("Area", storage.Parameter), "Area", new List<StatsValue> { StatsValue.Average, StatsValue.Stdev }),
                    new ExportType("Normalized data (Height)", new LegacyQuantValueAccessor("Normalized height", storage.Parameter), "NormalizedHeight", new List<StatsValue> { StatsValue.Average, StatsValue.Stdev }, isNormalized),
                    new ExportType("Normalized data (Area)", new LegacyQuantValueAccessor("Normalized area", storage.Parameter), "NormalizedArea", new List<StatsValue> { StatsValue.Average, StatsValue.Stdev }, isNormalized),
                    new ExportType("Peak ID", new LegacyQuantValueAccessor("ID", storage.Parameter), "PeakID"),
                    new ExportType("m/z", new LegacyQuantValueAccessor("MZ", storage.Parameter), "Mz"),
                    new ExportType("Mobility", new LegacyQuantValueAccessor("Mobility", storage.Parameter), "Mobility"),
                    new ExportType("CCS", new LegacyQuantValueAccessor("CCS", storage.Parameter), "CCS"),
                    new ExportType("S/N", new LegacyQuantValueAccessor("SN", storage.Parameter), "SN"),
                    new ExportType("MS/MS included", new LegacyQuantValueAccessor("MSMS", storage.Parameter), "MsmsIncluded"),
                    new ExportType("Identification method", new AnnotationMethodAccessor(), "IdentificationMethod"),
                },
                new[]
                {
                    ExportspectraType.deconvoluted,
                },
                peakSpotSupplyer);
            var spectraGroup = new AlignmentSpectraExportGroupModel(
                new[]
                {
                    ExportspectraType.deconvoluted,
                },
                peakSpotSupplyer,
                new AlignmentSpectraExportFormat("Msp", "msp", new AlignmentMspExporter(storage.DataBaseMapper, storage.Parameter)),
                new AlignmentSpectraExportFormat("Mgf", "mgf", new AlignmentMgfExporter()),
                new AlignmentSpectraExportFormat("Mat", "mat", new AlignmentMatExporter(storage.DataBaseMapper, storage.Parameter)));
            var spectraAndReference = new AlignmentMatchedSpectraExportModel(peakSpotSupplyer, storage.DataBaseMapper, analysisFileBeanModelCollection.IncludedAnalysisFiles, CompoundSearcherCollection.BuildSearchers(storage.DataBases, storage.DataBaseMapper));

            AlignmentResultExportModel = new AlignmentResultExportModel(new IAlignmentResultExportModel[] { peakGroup, spectraGroup, spectraAndReference, }, alignmentFilesForExport, peakSpotSupplyer, storage.Parameter.DataExportParam, broker);

            ParameterExportModel = new ParameterExportModel(storage.DataBases, storage.Parameter, broker);
        }

        public ImmsAnalysisModel? AnalysisModel {
            get => _analysisModel;
            set {
                var old = _analysisModel;
                if (SetProperty(ref _analysisModel, value)) {
                    old?.Dispose();
                }
            }
        }
        private ImmsAnalysisModel? _analysisModel;

        public ImmsAlignmentModel? AlignmentModel {
            get => _alignmentModel;
            set {
                var old = _alignmentModel;
                if (SetProperty(ref _alignmentModel, value)) {
                    old?.Dispose();
                }
            }
        }
        private ImmsAlignmentModel? _alignmentModel;

        public PeakFilterModel PeakFilterModel { get; }

        public IDataProviderFactory<AnalysisFileBean> ProviderFactory { get; }
        public AlignmentResultExportModel AlignmentResultExportModel { get; }
        public ParameterExportModel ParameterExportModel { get; }
        public StudyContextModel StudyContext { get; }

        public override Task RunAsync(ProcessOption option, CancellationToken token) {
            // Run PeakPick and Identification
            if (option.HasFlag(ProcessOption.Identification | ProcessOption.PeakSpotting)) {
                if (!ProcessPeakPickAndAnnotation(_storage)) {
                    return Task.CompletedTask;
                }
            }
            else if (option.HasFlag(ProcessOption.Identification)) {
                if (!ProcessAnnotation(_storage))
                    return Task.CompletedTask;
            }

            // Run Alignment
            if (option.HasFlag(ProcessOption.Alignment)) {
                if (!ProcessAlignment(_storage))
                    return Task.CompletedTask;
            }

            return LoadAnalysisFileAsync(AnalysisFileModelCollection.AnalysisFiles.FirstOrDefault(), token);
        }

        private bool ProcessPeakPickAndAnnotation(IMsdialDataStorage<MsdialImmsParameter> storage) {
            var request = new ProgressBarMultiContainerRequest(
                async vm =>
                {
                    var usable = Math.Max(_storage.Parameter.ProcessBaseParam.UsableNumThreads / 2, 1);
                    var processor = new FileProcess(storage, null, null, _matchResultEvaluator);
                    await processor.RunAllAsync(
                        storage.AnalysisFiles,
                        storage.AnalysisFiles.Select(ProviderFactory.Create),
                        vm.ProgressBarVMs.Select(pbvm => (Action<int>)((int v) => pbvm.CurrentValue = v)),
                        usable,
                        vm.Increment)
                    .ConfigureAwait(false);
                },
                storage.AnalysisFiles.Select(file => file.AnalysisFileName).ToArray());
            _broker.Publish(request);

            return request.Result ?? false;
        }

        private bool ProcessAnnotation(IMsdialDataStorage<MsdialImmsParameter> storage) {
            var request = new ProgressBarMultiContainerRequest(
                async vm =>
                {
                    var usable = Math.Max(_storage.Parameter.ProcessBaseParam.UsableNumThreads / 2, 1);
                    var processor = new FileProcess(storage, null, null, _matchResultEvaluator);
                    await processor.AnnotateAllAsync(
                        storage.AnalysisFiles,
                        storage.AnalysisFiles.Select(ProviderFactory.Create),
                        vm.ProgressBarVMs.Select(pbvm => (Action<int>)((int v) => pbvm.CurrentValue = v)),
                        usable,
                        vm.Increment)
                    .ConfigureAwait(false);
                },
                storage.AnalysisFiles.Select(file => file.AnalysisFileName).ToArray());
            _broker.Publish(request);

            return request.Result ?? false;
        }

        private bool ProcessAlignment(IMsdialDataStorage<MsdialImmsParameter> storage) {
            var request = new ProgressBarRequest("Process alignment..", isIndeterminate: true,
                async _ =>
                {
                    var factory = new ImmsAlignmentProcessFactory(storage, _matchResultEvaluator);
                    var aligner = factory.CreatePeakAligner();
                    aligner.ProviderFactory = ProviderFactory; // TODO: I'll remove this later.
                    var alignmentFileModel = AlignmentFiles.Files.Last();
                    var result = await Task.Run(() => alignmentFileModel.RunAlignment(aligner, CHROMATOGRAM_SPOT_SERIALIZER)).ConfigureAwait(false);
                    var tasks = new[]
                    {
                        alignmentFileModel.SaveAlignmentResultAsync(result),
                        alignmentFileModel.SaveMSDecResultsAsync(alignmentFileModel.LoadMSDecResultsFromEachFiles(result.AlignmentSpotProperties)),
                    };
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                });
            _broker.Publish(request);
            return request.Result ?? false;
        }

        protected override IAnalysisModel LoadAnalysisFileCore(AnalysisFileBeanModel analysisFile) {
            if (AnalysisModel != null) {
                AnalysisModel.Dispose();
                Disposables.Remove(AnalysisModel);
            }

            var provider = ProviderFactory.Create(analysisFile.File);
            AnalysisModel = new ImmsAnalysisModel(
                analysisFile,
                provider, _matchResultEvaluator,
                _storage.DataBases,
                _storage.DataBaseMapper,
                _storage.Parameter,
                PeakFilterModel,
                _projectBaseParameter,
                _broker)
            .AddTo(Disposables);
            return AnalysisModel;
        }

        protected override IAlignmentModel LoadAlignmentFileCore(AlignmentFileBeanModel alignmentFileModel) {
            if (AlignmentModel != null) {
                AlignmentModel.Dispose();
                Disposables.Remove(AlignmentModel);
            }

            return AlignmentModel = new ImmsAlignmentModel(
                alignmentFileModel,
                AnalysisFileModelCollection,
                _matchResultEvaluator,
                _storage.DataBases,
                _storage.DataBaseMapper,
                _peakSpotFiltering,
                PeakFilterModel,
                _projectBaseParameter,
                _storage.Parameter,
                _storage.AnalysisFiles,
                _broker)
            .AddTo(Disposables);
        }

        public AnalysisResultExportModel CreateExportAnalysisResult() {
            var container = _storage;
            var spectraTypes = new List<SpectraType>
            {
                new SpectraType(
                    ExportspectraType.deconvoluted,
                    new ImmsAnalysisMetadataAccessor(container.DataBaseMapper, container.Parameter, ExportspectraType.deconvoluted),
                    ProviderFactory),
                //new SpectraType(
                //    ExportspectraType.centroid,
                //    new ImmsAnalysisMetadataAccessor(container.DataBaseMapper, container.Parameter, ExportspectraType.centroid)),
                //new SpectraType(
                //    ExportspectraType.profile,
                //    new ImmsAnalysisMetadataAccessor(container.DataBaseMapper, container.Parameter, ExportspectraType.profile)),
            };
            var spectraFormats = new[]
            {
                new SpectraFormat(ExportSpectraFileFormat.txt, new AnalysisCSVExporterFactory(separator: "\t")),
            };
            var models = new IMsdialAnalysisExport[]
            {
                new MsdialAnalysisTableExportModel(spectraTypes, spectraFormats),
                new SpectraTypeSelectableMsdialAnalysisExportModel(new Dictionary<ExportspectraType, IAnalysisExporter<ChromatogramPeakFeatureCollection>> {
                    [ExportspectraType.deconvoluted] = new AnalysisMspExporter(_storage.DataBaseMapper, _storage.Parameter),
                    [ExportspectraType.centroid] = new AnalysisMspExporter(_storage.DataBaseMapper, _storage.Parameter, file => new CentroidMsScanPropertyLoader(ProviderFactory.Create(file), _storage.Parameter.MS2DataType)),
                })
                {
                    FilePrefix = "Msp",
                    FileSuffix = "msp",
                    Label = "Nist format (*.msp)"
                },
                new SpectraTypeSelectableMsdialAnalysisExportModel(new Dictionary<ExportspectraType, IAnalysisExporter<ChromatogramPeakFeatureCollection>> {
                    [ExportspectraType.deconvoluted] = new AnalysisMgfExporter(file => new MSDecLoader(file.DeconvolutionFilePath)),
                    [ExportspectraType.centroid] = new AnalysisMgfExporter(file => new CentroidMsScanPropertyLoader(ProviderFactory.Create(file), _storage.Parameter.MS2DataType)),
                })
                {
                    FilePrefix = "Mgf",
                    FileSuffix = "mgf",
                    Label = "MASCOT format (*.mgf)"
                },
                new MsdialAnalysisMassBankRecordExportModel(_storage.Parameter.ProjectParam, StudyContext),
            };
            return new AnalysisResultExportModel(AnalysisFileModelCollection, _storage.Parameter.ProjectParam.ProjectFolderPath, models);
        }

        public CheckChromatogramsModel? PrepareChromatograms(bool tic, bool bpc, bool highestEic) {
            var analysisModel = AnalysisModel;
            if (analysisModel is null) {
                return null;
            }

            var loadChromatogramsUsecase = analysisModel.LoadChromatogramsUsecase();
            loadChromatogramsUsecase.InsertTic = tic;
            loadChromatogramsUsecase.InsertBpc = bpc;
            loadChromatogramsUsecase.InsertHighestEic = highestEic;
            var model = new CheckChromatogramsModel(loadChromatogramsUsecase, _storage.Parameter.AdvancedProcessOptionBaseParam);
            model.Update();
            return model;
        }
    }
}
