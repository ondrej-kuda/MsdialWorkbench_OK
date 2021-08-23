﻿using CompMs.App.Msdial.Model.Setting;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialLcMsApi.Algorithm.Annotation;
using System;

namespace CompMs.App.Msdial.Model.Lcms
{
    sealed class LcmsMspAnnotationSettingModel : MspAnnotationSettingModel
    {
        public LcmsMspAnnotationSettingModel(DataBaseAnnotationSettingModelBase other)
            : base(other) {
            
        }

        protected override ISerializableAnnotatorContainer BuildCore(ParameterBase parameter, MoleculeDataBase molecules) {
            return new DatabaseAnnotatorContainer(
                new LcmsMspAnnotator(molecules, Parameter, parameter.ProjectParam.TargetOmics, AnnotatorID),
                molecules,
                Parameter);
        }
    }
}
