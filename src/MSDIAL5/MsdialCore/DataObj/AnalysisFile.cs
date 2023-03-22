﻿using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using CompMs.Common.MessagePack;
using CompMs.MsdialCore.Utility;
using CompMs.RawDataHandler.Core;
using MessagePack;
using System.Collections.Generic;
using System.IO;

namespace CompMs.MsdialCore.DataObj {
    [MessagePackObject]
    public class AnalysisFileBean : IFileBean
    {
        [Key(0)]
        public string AnalysisFilePath { get; set; } = string.Empty;
        [Key(1)]
        public string AnalysisFileName { get; set; } = string.Empty;
        [Key(2)]
        public AnalysisFileType AnalysisFileType { get; set; }
        [Key(3)]
        public string AnalysisFileClass { get; set; } = string.Empty;
        [Key(4)]
        public int AnalysisFileAnalyticalOrder { get; set; }
        [Key(5)]
        public int AnalysisFileId { get; set; }
        [Key(6)]
        public bool AnalysisFileIncluded { get; set; }
        [Key(7)]
        public string DeconvolutionFilePath { get; set; } = string.Empty;// *.dcl
        [Key(8)]
        public List<string> DeconvolutionFilePathList { get; set; } = new List<string>(); // *.dcl
        [Key(9)]
        public string PeakAreaBeanInformationFilePath { get; set; } = string.Empty; // *.pai
        [Key(10)]
        public string RiDictionaryFilePath { get; set; } = string.Empty;
        [Key(11)]
        public int AnalysisBatch { get; set; } = 1;
        [Key(12)]
        public double ResponseVariable { get; set; } = 0; // for PLS
        [Key(13)]
        public double DilutionFactor { get; set; } = 1.0;
        [Key(14)]
        public string AnalysisFileSuperClass { get; set; } = string.Empty;
        [Key(15)]
        public RetentionTimeCorrectionBean RetentionTimeCorrectionBean { get; set; } = new RetentionTimeCorrectionBean();
        [Key(16)]
        public ChromatogramPeaksDataSummaryDto ChromPeakFeaturesSummary { get; set; } = new ChromatogramPeaksDataSummaryDto();
        [Key(17)]
        public string ProteinAssembledResultFilePath { get; set; } // *.prf
        [Key(18)]
        public AcquisitionType AcquisitionType { get; set; }

        public AnalysisFileBean() {

        }

        int IFileBean.FileID => AnalysisFileId;
        string IFileBean.FileName => AnalysisFileName;
        string IFileBean.FilePath => AnalysisFilePath;

        public void SaveSpectrumFeatures(List<SpectrumFeature> spectrumFeatures) {
            var name = Path.GetFileNameWithoutExtension(PeakAreaBeanInformationFilePath);
            var path = Path.Combine(Path.GetDirectoryName(PeakAreaBeanInformationFilePath), name + ".sfi"); // *.sfi
            MessagePackDefaultHandler.SaveLargeListToFile(spectrumFeatures, path);
        }

        public RawMeasurement LoadRawMeasurement(bool isImagingMsData, bool isGuiProcess, int retry, int sleepMilliSeconds) {
            return DataAccess.LoadMeasurement(this, isImagingMsData, isGuiProcess, retry, sleepMilliSeconds);
        }

        public MaldiFrameLaserInfo GetMaldiFrameLaserInfo() {
            return new RawDataAccess(AnalysisFilePath, 0, false, true, true).GetMaldiFrameLaserInfo();
        }

        public List<MaldiFrameInfo> GetMaldiFrames() {
            return new RawDataAccess(AnalysisFilePath, 0, false, true, true).GetMaldiFrames();
        }
    }
}
