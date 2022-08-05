﻿using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CompMs.App.Msdial.Model.DataObj
{
    internal sealed class PeptideGroupModel : BindableBase
    {
        private readonly ObservableCollection<ProteinModel> _proteins;
        private readonly ObservableCollection<PeptideModel> _uniquePeptides;
        private readonly ObservableCollection<PeptideModel> _peptides;
        private readonly ProteinGroup _proteinGroup;

        public PeptideGroupModel(ProteinGroup proteinGroup, IReadOnlyList<ChromatogramPeakFeatureModel> spots)
        {
            _proteins = new ObservableCollection<ProteinModel>(proteinGroup.ProteinMsResults.Select(result => new ProteinModel(result, spots)));
            //_uniquePeptides = new ObservableCollection<PeptideModel>(_proteins.SelectMany(protein => protein.UniquePeptides));
            _peptides = new ObservableCollection<PeptideModel>(_proteins.SelectMany(protein => protein.Peptides));
        }

        public PeptideGroupModel(ProteinGroup proteinGroup, IReadOnlyList<AlignmentSpotPropertyModel> spots)
        {
            _proteins = new ObservableCollection<ProteinModel>(proteinGroup.ProteinMsResults.Select(result => new ProteinModel(result, spots)));
            //_uniquePeptides = new ObservableCollection<PeptideModel>(_proteins.SelectMany(protein => protein.UniquePeptides));
            _peptides = new ObservableCollection<PeptideModel>(_proteins.SelectMany(protein => protein.Peptides));
        }

        //public int NoOfUniquePeptides => _uniquePeptides.Count;
        public int NoOfPeptides => _peptides.Count;


    }
}
