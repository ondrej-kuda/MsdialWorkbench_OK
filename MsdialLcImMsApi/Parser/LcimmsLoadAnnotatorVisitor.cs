﻿using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Proteomics.DataObj;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialLcImMsApi.Algorithm.Annotation;
using System;

namespace CompMs.MsdialLcImMsApi.Parser
{
    internal sealed class LcimmsLoadAnnotatorVisitor : ILoadAnnotatorVisitor
    {
        private readonly ParameterBase _parameter;

        public LcimmsLoadAnnotatorVisitor(ParameterBase parameter) {
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public ISerializableAnnotator<IAnnotationQuery<MsScanMatchResult>, MoleculeMsReference, MsScanMatchResult, MoleculeDataBase> Visit(StandardRestorationKey key, MoleculeDataBase database) {
            if (key.SourceType.HasFlag(SourceType.MspDB)) {
                return new LcimmsMspAnnotator(database, key.Parameter, _parameter.ProjectParam.TargetOmics, key.Key, key.Priority);
            }
            else if (key.SourceType.HasFlag(SourceType.TextDB)) {
                return new LcimmsTextDBAnnotator(database, key.Parameter, key.Key, key.Priority);
            }
            throw new NotSupportedException(key.SourceType.ToString());
        }

        public ISerializableAnnotator<IAnnotationQuery<MsScanMatchResult>, MoleculeMsReference, MsScanMatchResult, MoleculeDataBase> Visit(MspDbRestorationKey key, MoleculeDataBase database) {
            return new LcimmsMspAnnotator(database, _parameter.RefSpecMatchBaseParam.MspSearchParam, _parameter.ProjectParam.TargetOmics, key.Key, key.Priority);
        }

        public ISerializableAnnotator<IAnnotationQuery<MsScanMatchResult>, MoleculeMsReference, MsScanMatchResult, MoleculeDataBase> Visit(TextDbRestorationKey key, MoleculeDataBase database) {
            return new LcimmsTextDBAnnotator(database, _parameter.RefSpecMatchBaseParam.TextDbSearchParam, key.Key, key.Priority);
        }

        public ISerializableAnnotator<IPepAnnotationQuery, PeptideMsReference, MsScanMatchResult, ShotgunProteomicsDB> Visit(ShotgunProteomicsRestorationKey key, ShotgunProteomicsDB database) {
            throw new NotSupportedException();
        }

        public ISerializableAnnotator<(IAnnotationQuery<MsScanMatchResult>, MoleculeMsReference), MoleculeMsReference, MsScanMatchResult, EadLipidDatabase> Visit(EadLipidDatabaseRestorationKey key, EadLipidDatabase database) {
            return new EadLipidAnnotator(database, key.Key, key.Priority, key.MsRefSearchParameter);
        }
    }
}
