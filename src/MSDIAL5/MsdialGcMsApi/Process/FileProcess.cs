﻿using CompMs.Common.Enum;
using CompMs.Common.Interfaces;
using CompMs.Common.Utility;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Utility;
using CompMs.MsdialGcMsApi.Algorithm;
using CompMs.MsdialGcMsApi.Parameter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.MsdialGcMsApi.Process
{
    public sealed class FileProcess : IFileProcessor {
        private static readonly double PROCESS_START = 0d;
        private static readonly double PROCESS_END = 100d;
        private static readonly double PEAKSPOTTING_START = 0d;
        private static readonly double PEAKSPOTTING_END = 30d;
        private static readonly double DECONVOLUTION_START = 30d;
        private static readonly double DECONVOLUTION_END = 60d;
        private static readonly double ANNOTATION_START = 60d;
        private static readonly double ANNOTATION_END = 90d;

        private readonly RiCompoundType _riCompoundType;
        private readonly IDataProviderFactory<AnalysisFileBean> _providerFactory;
        private readonly Dictionary<int, RiDictionaryInfo> _riDictionaryInfo;
        private readonly PeakSpotting _peakSpotting;
        private readonly Ms1Dec _ms1Deconvolution;
        private readonly Annotation _annotation;

        public FileProcess(IDataProviderFactory<AnalysisFileBean> providerFactory, IMsdialDataStorage<MsdialGcmsParameter> storage, CalculateMatchScore calculateMatchScore) {
            if (storage is null || storage.Parameter is null) {
                throw new ArgumentNullException(nameof(storage));
            }
            _riCompoundType = storage.Parameter.RiCompoundType;
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _riDictionaryInfo = storage.Parameter.FileIdRiInfoDictionary;
            _peakSpotting = new PeakSpotting(storage.IupacDatabase, storage.Parameter);
            _ms1Deconvolution = new Ms1Dec(storage.Parameter);
            _annotation = new Annotation(calculateMatchScore, storage.Parameter);
        }

        public async Task RunAsync(AnalysisFileBean analysisFile, Action<int> reportAction, CancellationToken token = default) {
            reportAction?.Invoke((int)PROCESS_START);
            var carbon2RtDict = analysisFile.GetRiDictionary(_riDictionaryInfo);
            var riHandler = carbon2RtDict is null ? null : new RetentionIndexHandler(_riCompoundType, carbon2RtDict);

            Console.WriteLine("Loading spectral information");
            var provider = _providerFactory.Create(analysisFile);
            token.ThrowIfCancellationRequested();

            // feature detections
            Console.WriteLine("Peak picking started");
            var reportSpotting = ReportProgress.FromRange(reportAction, PEAKSPOTTING_START, PEAKSPOTTING_END);
            var chromPeakFeatures = _peakSpotting.Run(analysisFile, provider, reportSpotting, token);
            SetRetentionIndex(chromPeakFeatures, riHandler);
            await analysisFile.SetChromatogramPeakFeaturesSummaryAsync(provider, chromPeakFeatures, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            // chrom deconvolutions
            Console.WriteLine("Deconvolution started");
            var reportDeconvolution = ReportProgress.FromRange(reportAction, DECONVOLUTION_START, DECONVOLUTION_END);
            var spectra = await provider.LoadMsSpectrumsAsync(token).ConfigureAwait(false);
            var msdecResults = _ms1Deconvolution.GetMSDecResults(spectra, chromPeakFeatures, reportDeconvolution);
            SetRetentionIndex(msdecResults, riHandler);
            token.ThrowIfCancellationRequested();

            // annotations
            Console.WriteLine("Annotation started");
            var reportAnnotation = ReportProgress.FromRange(reportAction, ANNOTATION_START, ANNOTATION_END);
            var annotatedMSDecResults = _annotation.MainProcess(msdecResults, reportAnnotation);
            token.ThrowIfCancellationRequested();

            var spectrumFeatureCollection = _ms1Deconvolution.GetSpectrumFeaturesByQuantMassInformation(analysisFile, spectra, annotatedMSDecResults);
            SetRetentionIndex(spectrumFeatureCollection, riHandler);

            // save
            analysisFile.SaveChromatogramPeakFeatures(chromPeakFeatures);
            analysisFile.SaveMsdecResultWithAnnotationInfo(msdecResults);
            analysisFile.SaveSpectrumFeatures(spectrumFeatureCollection);

            reportAction?.Invoke((int)PROCESS_END);
        }

        public async Task AnnotateAsync(AnalysisFileBean analysisFile, Action<int> reportAction, CancellationToken token = default) {
            reportAction?.Invoke((int)PROCESS_START);
            var carbon2RtDict = analysisFile.GetRiDictionary(_riDictionaryInfo);
            var riHandler = carbon2RtDict is null ? null : new RetentionIndexHandler(_riCompoundType, carbon2RtDict);

            await Task.Yield();
            Console.WriteLine("Loading spectral information");
            var provider = _providerFactory.Create(analysisFile);
            token.ThrowIfCancellationRequested();
            var spectraTask = provider.LoadMsSpectrumsAsync(token);
            var mSDecResults = analysisFile.LoadMsdecResultWithAnnotationInfo();

            // annotations
            Console.WriteLine("Annotation started");
            var reportAnnotation = ReportProgress.FromRange(reportAction, ANNOTATION_START, ANNOTATION_END);
            var annotatedMSDecResults = _annotation.MainProcess(mSDecResults, reportAnnotation);
            token.ThrowIfCancellationRequested();

            var spectra = await spectraTask.ConfigureAwait(false);
            var spectrumFeatureCollection = _ms1Deconvolution.GetSpectrumFeaturesByQuantMassInformation(analysisFile, spectra, annotatedMSDecResults);
            SetRetentionIndex(spectrumFeatureCollection, riHandler);

            // save
            analysisFile.SaveMsdecResultWithAnnotationInfo(mSDecResults);
            analysisFile.SaveSpectrumFeatures(spectrumFeatureCollection);
            reportAction?.Invoke((int)PROCESS_END);
        }

        public static void Run(AnalysisFileBean file, IMsdialDataStorage<MsdialGcmsParameter> container, bool isGuiProcess = false, Action<int> reportAction = null, CancellationToken token = default) {
            var providerFactory = new StandardDataProviderFactory(isGuiProcess: isGuiProcess);
            new FileProcess(providerFactory, container, new CalculateMatchScore(container.DataBases.MetabolomicsDataBases.FirstOrDefault(), container.Parameter.MspSearchParam, container.Parameter.RetentionType)).RunAsync(file, reportAction, token).Wait();
        }

        private void SetRetentionIndex(IReadOnlyList<IChromatogramPeakFeature> peaks, RetentionIndexHandler riHandler) {
            if (riHandler is null) {
                return;
            }
            foreach (var peak in peaks) {
                peak.ChromXsLeft.RI = riHandler.Convert(peak.ChromXsLeft.RT);
                peak.ChromXsTop.RI = riHandler.Convert(peak.ChromXsTop.RT);
                peak.ChromXsRight.RI = riHandler.Convert(peak.ChromXsRight.RT);
            }
        }

        private void SetRetentionIndex(IReadOnlyList<MSDecResult> results, RetentionIndexHandler riHandler) {
            if (riHandler is null) {
                return;
            }
            foreach (var result in results) {
                result.ChromXs.RI = riHandler.Convert(result.ChromXs.RT);
                foreach (var chrom in result.ModelPeakChromatogram.Select(p => p.ChromXs)) {
                    chrom.RI = riHandler.Convert(chrom.RT);
                }
            }
        }

        private void SetRetentionIndex(SpectrumFeatureCollection spectrumFeatures, RetentionIndexHandler riHandler) {
            if (riHandler is null) {
                return;
            }
            foreach (var spectrumFeature in spectrumFeatures.Items) {
                var peakFeature = spectrumFeature.QuantifiedChromatogramPeak.PeakFeature;
                peakFeature.ChromXsLeft.RI = riHandler.Convert(peakFeature.ChromXsLeft.RT);
                peakFeature.ChromXsTop.RI = riHandler.Convert(peakFeature.ChromXsTop.RT);
                peakFeature.ChromXsRight.RI = riHandler.Convert(peakFeature.ChromXsRight.RT);
            }
        }
    }
}
