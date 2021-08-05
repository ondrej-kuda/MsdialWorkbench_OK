﻿using CompMs.Common.DataObj;
using CompMs.Common.Proteomics.DataObj;
using CompMs.Common.Proteomics.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompMs.Common.Proteomics.Function {
    
    public sealed class ProteinDigestion {
        private ProteinDigestion() { }

        public static List<Peptide> GetDigestedPeptideSequences(string proteinSequence, List<string> cleavagesites, string database = "", int databaseID = -1) {
            var peptides = new List<Peptide>();
            var peptideSequence = string.Empty;
            var twoletterAAs = string.Empty;
            var start = 0;
            var end = 0;
            for (int i = 0; i < proteinSequence.Length - 1; i++) {
                peptideSequence += proteinSequence[i];
                twoletterAAs = proteinSequence[i].ToString() + proteinSequence[i + 1].ToString();

                if (cleavagesites.Contains(twoletterAAs)) {
                    end = i;
                    var peptide = new Peptide() { DatabaseOrigin = database, DatabaseOriginID = databaseID, Sequence = peptideSequence, Position = new Range(start, end) };
                    peptide.IsProteinNterminal = start == 0 ? true : false;
                    peptide.IsProteinCterminal = end == proteinSequence.Length - 1 ? true : false;
                    peptides.Add(peptide);

                    peptideSequence = string.Empty;
                    start = i + 1;
                    end = i + 1;
                }
            }

            return peptides;
        }

        public static List<Peptide> GetDigestedPeptideSequences(string proteinSequence, List<string> cleavagesites, 
            int maxMissedCleavage = 2, string database = "", int databaseID = -1, int minimumPeptideLength = 7) {
            var peptideSequence = string.Empty;
            var twoletterAAs = string.Empty;
            var start = 0;
            var end = 0;
            var peptides = GetDigestedPeptideSequences(proteinSequence, cleavagesites, database, databaseID);
            var currentTotalCount = peptides.Count;

            for (int i = 1; i <= maxMissedCleavage; i++) {
                for (int j = 0; j < currentTotalCount - i; j++) {
                    start = peptides[j].Position.Start;
                    end = peptides[j + i].Position.End;
                    peptideSequence = string.Empty;
                    for (int k = j; k <= j + i; k++) {
                        peptideSequence += peptides[k].Sequence;
                    }
                    var peptide = new Peptide() { DatabaseOrigin = database, DatabaseOriginID = databaseID, Sequence = peptideSequence, Position = new Range(start, end) };
                    peptide.IsProteinNterminal = start == 0 ? true : false;
                    peptide.IsProteinCterminal = end == proteinSequence.Length - 1 ? true : false;

                    if (peptide.Sequence.Length >= minimumPeptideLength)
                        peptides.Add(peptide);
                }
            }

            return peptides;
        }

        public static List<string> GetCleavageSites(List<string> selectedEnzymes) {
            var eParser = new EnzymesXmlRefParser();
            eParser.Read();

            var enzymes = eParser.Enzymes;
            return GetCleavageSites(enzymes, selectedEnzymes);
        }

        public static List<string> GetCleavageSites(List<Enzyme> enzymes, List<string> selectedEnzymes) {
            var sites = new List<string>();
            var sEnzymes = new List<Enzyme>();
            foreach (var enzymeString in selectedEnzymes) {
                foreach (var enzymeObj in enzymes) {
                    if (enzymeString == enzymeObj.Title) {
                        sEnzymes.Add(enzymeObj);
                        break;
                    }
                }
            }
                 
            foreach (var enzyme in sEnzymes) {
                foreach (var site in enzyme.SpecificityList) {
                    sites.Add(site);
                }
            }

            return sites;
        }

        public static List<string> GetCleavageSites(List<Enzyme> enzymes) {
            var sites = new List<string>();
            var sEnzymes = enzymes.Where(n => n.IsSelected).ToList();

            foreach (var enzyme in sEnzymes) {
                foreach (var site in enzyme.SpecificityList) {
                    sites.Add(site);
                }
            }

            return sites;
        }
    }
}
