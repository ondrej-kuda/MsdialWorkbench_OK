﻿using CompMs.App.Msdial.Model.Setting;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialLcMsApi.Algorithm.Annotation;

namespace CompMs.App.Msdial.Model.Lcms
{
    sealed class LcmsLbmAnnotationSettingModel : LbmAnnotationSettingModel
    {
        public LcmsLbmAnnotationSettingModel(DataBaseAnnotationSettingModelBase other, ParameterBase parameter)
            : base(other, parameter) {

        }

        protected override ISerializableAnnotatorContainer BuildCore(ProjectBaseParameter projectParameter, MoleculeDataBase molecules) {
            return new DatabaseAnnotatorContainer(
                new LcmsMspAnnotator(molecules, Parameter, projectParameter.TargetOmics, AnnotatorID),
                molecules,
                Parameter);
        }
    }
}
