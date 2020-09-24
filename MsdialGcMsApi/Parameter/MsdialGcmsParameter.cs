﻿using CompMs.Common.Enum;
using CompMs.MsdialCore.Parameter;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace CompMs.MsdialGcMsApi.Parameter {
    [MessagePackObject]
    public class MsdialGcmsParameter : ParameterBase {
        
        [Key(150)]
        public string RiDictionaryFilePath { get; set; } = string.Empty;
        [Key(151)]
        public RiCompoundType RiCompoundType { get; set; } = RiCompoundType.Alkanes;
        [Key(152)]
        public RetentionType RetentionType { get; set; } = RetentionType.RT;
        [Key(153)]
        public AlignmentIndexType AlignmentIndexType { get; set; } = AlignmentIndexType.RT;
        [Key(154)]
        public float RetentionIndexAlignmentTolerance { get; set; } = 20;
        [Key(155)]
        public bool IsReplaceQuantmassByUserDefinedValue { get; set; } = false;
        [Key(156)]
        public bool IsRepresentativeQuantMassBasedOnBasePeakMz { get; set; } = false;

        public MsdialGcmsParameter() { this.MachineCategory = MachineCategory.GCMS; }

        public override List<string> ParametersAsText() {
            var pStrings = base.ParametersAsText();

            pStrings.Add("\r\n");
            pStrings.Add("# GCMS specific parameters");
            pStrings.Add(String.Join(": ", new string[] { "RI dictionary file path", RiDictionaryFilePath.ToString() }));
            pStrings.Add(String.Join(": ", new string[] { "RI compound type", RiCompoundType.ToString() }));
            pStrings.Add(String.Join(": ", new string[] { "Retention type", RetentionType.ToString() }));
            pStrings.Add(String.Join(": ", new string[] { "Alignment index type", AlignmentIndexType.ToString() }));
            pStrings.Add(String.Join(": ", new string[] { "Retention index alignment tolerance", RetentionIndexAlignmentTolerance.ToString() }));
            pStrings.Add(String.Join(": ", new string[] { "Replace quant mass by user defined value", IsReplaceQuantmassByUserDefinedValue.ToString() }));
            pStrings.Add(String.Join(": ", new string[] { "Is quant mass based on base peak mz", IsRepresentativeQuantMassBasedOnBasePeakMz.ToString() }));

            return pStrings;
        }
    }
}
