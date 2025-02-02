﻿using CompMs.App.Msdial.Model.Core;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Export;
using CompMs.Common.Enum;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialImmsCore.Export;
using CompMs.MsdialImmsCore.Parameter;
using CompMs.MsdialImmsCore.Process;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.ImagingImms
{
    internal sealed class ImagingImmsMethodModel : MethodModelBase, IMethodModel
    {
        private readonly IMsdialDataStorage<MsdialImmsParameter> _storage;
        private readonly IMessageBroker _broker;
        private readonly FilePropertiesModel _projectBaseParameter;
        private readonly FacadeMatchResultEvaluator _evaluator;
        private readonly IDataProviderFactory<AnalysisFileBeanModel> _providerFactory;

        public ImagingImmsMethodModel(AnalysisFileBeanModelCollection analysisFileBeanModelCollection, AlignmentFileBeanModelCollection alignmentFileBeanModelCollection, IMsdialDataStorage<MsdialImmsParameter> storage, FilePropertiesModel projectBaseParameter, StudyContextModel studyContext, IMessageBroker broker)
            : base(analysisFileBeanModelCollection, alignmentFileBeanModelCollection, projectBaseParameter) {
            _storage = storage;
            _projectBaseParameter = projectBaseParameter;
            _broker = broker;
            _projectBaseParameter = projectBaseParameter;
            StudyContext = studyContext;
            _evaluator = FacadeMatchResultEvaluator.FromDataBases(storage.DataBases);
            _providerFactory = new StandardDataProviderFactory().ContraMap((AnalysisFileBeanModel file) => file.File.LoadRawMeasurement(true, true, 5, 5000));
            ImageModels = new ObservableCollection<ImagingImmsImageModel>();
            Image = ImageModels.FirstOrDefault();

            ParameterExporModel = new ParameterExportModel(storage.DataBases, storage.Parameter, broker);
        }

        public ObservableCollection<ImagingImmsImageModel> ImageModels { get; }

        public ImagingImmsImageModel? Image {
            get => _image;
            set => SetProperty(ref _image, value);
        }
        private ImagingImmsImageModel? _image;

        public ParameterExportModel ParameterExporModel { get; }
        public StudyContextModel StudyContext { get; }

        public override async Task RunAsync(ProcessOption option, CancellationToken token) {
            var files = AnalysisFileModelCollection.IncludedAnalysisFiles;
            if (option.HasFlag(ProcessOption.Identification | ProcessOption.PeakSpotting)) {
                var processor = new FileProcess(_storage, null, null, _evaluator);
                await processor.RunAllAsync(files.Select(file => file.File), files.Select(_providerFactory.Create), Enumerable.Repeat<Action<int>?>(null, files.Count), 2, null).ConfigureAwait(false);
                foreach (var file in files) {
                    ImageModels.Add(new ImagingImmsImageModel(file, _storage, _evaluator, _providerFactory, _projectBaseParameter, _broker));
                }
            }
            else if (option.HasFlag(ProcessOption.Identification)) {
                var processor = new FileProcess(_storage, null, null, _evaluator);
                await processor.AnnotateAllAsync(files.Select(file => file.File), files.Select(_providerFactory.Create), Enumerable.Repeat<Action<int>?>(null, files.Count), 2, null).ConfigureAwait(false);
                foreach (var file in files) {
                    ImageModels.Add(new ImagingImmsImageModel(file, _storage, _evaluator, _providerFactory, _projectBaseParameter, _broker));
                }
            }
            await LoadAnalysisFileAsync(files.FirstOrDefault(), token).ConfigureAwait(false);
        }

        public override Task LoadAsync(CancellationToken token) {
            foreach (var file in AnalysisFileModelCollection.AnalysisFiles) {
                ImageModels.Add(new ImagingImmsImageModel(file, _storage, _evaluator, _providerFactory, _projectBaseParameter, _broker));
            }
            var analysisFile = AnalysisFileModelCollection.IncludedAnalysisFiles.FirstOrDefault();
            if (!(analysisFile is null)) {
                Image = ImageModels.FirstOrDefault(image => image.File == analysisFile);
            }
            return Task.CompletedTask;
        }

        protected override IAnalysisModel? LoadAnalysisFileCore(AnalysisFileBeanModel analysisFile) {
            Image = ImageModels.FirstOrDefault(image => image.File == analysisFile);
            return null;
        }

        protected override IAlignmentModel? LoadAlignmentFileCore(AlignmentFileBeanModel alignmentFileModel) {
            return null;
        }

        public override Task SaveAsync()
        {
            if (Image is null || Image.ImageResult is null)
            {
                return Task.CompletedTask;
            }

            return Image.ImageResult.SaveAsync();
        }

        public AnalysisResultExportModel CreateExportAnalysisModel() {
            var spectraTypes = new[]
            {
                new SpectraType(
                    ExportspectraType.deconvoluted,
                    new ImmsAnalysisMetadataAccessor(_storage.DataBaseMapper, _storage.Parameter, ExportspectraType.deconvoluted),
                    _providerFactory.ContraMap((AnalysisFileBean f) => new AnalysisFileBeanModel(f))),
                //new SpectraType(
                //    ExportspectraType.centroid,
                //    new ImmsAnalysisMetadataAccessor(_storage.DataBaseMapper, _storage.Parameter, ExportspectraType.centroid)),
                //new SpectraType(
                //    ExportspectraType.profile,
                //    new ImmsAnalysisMetadataAccessor(_storage.DataBaseMapper, _storage.Parameter, ExportspectraType.profile)),
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
                    [ExportspectraType.centroid] = new AnalysisMspExporter(_storage.DataBaseMapper, _storage.Parameter, file => new CentroidMsScanPropertyLoader(_storage.Parameter.ProviderFactoryParameter.Create().Create(file.LoadRawMeasurement(true, true, 5, 5000)), _storage.Parameter.MS2DataType)),
                })
                {
                    FilePrefix = "Msp",
                    FileSuffix = "msp",
                    Label = "Nist format (*.msp)"
                },
                new SpectraTypeSelectableMsdialAnalysisExportModel(new Dictionary<ExportspectraType, IAnalysisExporter<ChromatogramPeakFeatureCollection>> {
                    [ExportspectraType.deconvoluted] = new AnalysisMgfExporter(file => new MSDecLoader(file.DeconvolutionFilePath)),
                    [ExportspectraType.centroid] = new AnalysisMgfExporter(file => new CentroidMsScanPropertyLoader(_storage.Parameter.ProviderFactoryParameter.Create().Create(file.LoadRawMeasurement(true, true, 5, 5000)), _storage.Parameter.MS2DataType)),
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
    }
}
