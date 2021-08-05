﻿using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Extension;
using CompMs.Common.Proteomics.DataObj;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompMs.Common.Proteomics.Function {
    public sealed class PeptideCalc {
        private PeptideCalc() { }

        private static double OH = 17.002739652;
        private static double H = 1.00782503207;
        private static double H2O = 18.010564684;
       
        // N -> C, just return exactmass using default setting
        public static double Sequence2Mass(string sequence) {
            var mass = 0.0;
            var char2mass = AminoAcidObjUtility.OneChar2Mass;
            var offsetMass = OH + H2O * (sequence.Length - 2) + H; // N-terminal, internal amino acids, C-terminal
            for (int i = 0; i < sequence.Length; i++) {
                var aaChar = sequence[i];
                if (char2mass.ContainsKey(aaChar)) {
                    mass += char2mass[aaChar];
                }
            }
            return mass - offsetMass;
        }

        // just return peptide obj containing exactmass using default setting
        public static Peptide Sequence2Peptide(string sequence) {
            
            var formula = Sequence2Formula(sequence);
            return new Peptide() { Sequence = sequence, ExactMass = formula.Mass };
        }

        public static Peptide Sequence2Peptide(Peptide peptide) {
            var sequence = peptide.Sequence;
            var formula = Sequence2Formula(sequence);
            peptide.ExactMass = formula.Mass;
            return peptide;
        }

        public static List<Peptide> Sequence2Peptides(Peptide peptide, ModificationContainer container, int maxNumberOfModificationsPerPeptide = 5, double maxPeptideMass = 4600) {
            var fmPeptide = Sequence2PeptideByFixedModifications(peptide, container, maxPeptideMass);
            if (fmPeptide == null) return null;
            return Sequence2PeptidesByVariableModifications(peptide, container, maxNumberOfModificationsPerPeptide, maxPeptideMass);
        }

        public static Peptide Sequence2PeptideByFixedModifications(Peptide peptide, ModificationContainer container, double maxPeptideMass = 4600) {
            var sequence = peptide.Sequence;
            if (container.IsEmptyOrNull()) return Sequence2Peptide(peptide);

            var isProteinNTerminal = peptide.IsProteinNterminal;
            var isProteinCTerminal = peptide.IsProteinCterminal;
            var aaSequence = new List<AminoAcid>();
            
            for (int i = 0; i < sequence.Length; i++) {
                var modseq = new List<Modification>();
                var aa = GetAminoAcidByFixedModifications(peptide, modseq, container, i);
                aaSequence.Add(aa);
            }
           
            peptide.SequenceObj = aaSequence;
            var formula = CalculatePeptideFormula(aaSequence);
            if (formula.Mass > maxPeptideMass) return null;
            peptide.ExactMass = formula.Mass;
           
            return peptide;
        }

        public static AminoAcid GetAminoAcidByFixedModifications(Peptide peptide, List<Modification> modseq, ModificationContainer container, int index) {

            var isProteinNTerminal = peptide.IsProteinNterminal;
            var isProteinCTerminal = peptide.IsProteinCterminal;

            var sequence = peptide.Sequence;
            var aaChar = sequence[index];

            var isPeptideNTerminal = index == 0 ? true : false;
            var isPeptideCTerminal = index == sequence.Length - 1 ? true : false;

            return GetAminoAcidByFixedModifications(modseq, container, aaChar, isPeptideNTerminal, isPeptideCTerminal, isProteinNTerminal, isProteinCTerminal);
        }

        public static AminoAcid GetAminoAcidByFixedModifications(List<Modification> modseq, ModificationContainer container,
            char aaChar, bool isPeptideNTerminal, bool isPeptideCTerminal, bool isProteinNTerminal, bool isProteinCTerminal) {
            if (isPeptideNTerminal && isProteinNTerminal && container.ProteinNterm2FixedMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.ProteinNterm2FixedMod[aaChar]);
            }
            else if (isPeptideNTerminal && container.AnyNtermSite2FixedMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.AnyNtermSite2FixedMod[aaChar]);
            }

            if (!isPeptideNTerminal && !isPeptideCTerminal && container.NotCtermSite2FixedMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.NotCtermSite2FixedMod[aaChar]);
            }

            if (isPeptideCTerminal && isProteinCTerminal && container.ProteinCtermSite2FixedMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.ProteinCtermSite2FixedMod[aaChar]);
            }
            else if (isPeptideCTerminal && container.AnyCtermSite2FixedMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.AnyCtermSite2FixedMod[aaChar]);
            }

            if (container.AnywehereSite2FixedMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.AnywehereSite2FixedMod[aaChar]);
            }

            var compositions = ModificationUtility.GetModifiedCompositions(aaChar.ToString(), modseq);
            var code = compositions.Item1;
            if (!container.Code2AminoAcidObj.IsEmptyOrNull() && container.Code2AminoAcidObj.ContainsKey(code)) {
                return container.Code2AminoAcidObj[code];
            }
            else if (!container.Code2AminoAcidObj.IsEmptyOrNull() && code == string.Empty) {
                return container.Code2AminoAcidObj[aaChar.ToString()]; ;
            }
            else {
                var aa = new AminoAcid(container.AnywehereSite2FixedMod[aaChar].OriginalAA, compositions.Item1, compositions.Item2);
                return aa;
            }
            //var aa = new AminoAcid(container.AnywehereSite2FixedMod[aaChar].OriginalAA, compositions.Item1, compositions.Item2);
            //return aa;
        }

        /// <summary>
        /// peptide should be processed by Sequence2PeptideByFixedModifications before using this method
        /// </summary>
        /// <param name="peptide"></param>
        /// <param name="container"></param>
        /// <param name="maxNumberOfModificationsPerPeptide"></param>
        /// <returns></returns>
        public static List<Peptide> Sequence2PeptidesByVariableModifications(Peptide peptide, ModificationContainer container, int maxNumberOfModificationsPerPeptide = 5, double maxPeptideMass = 4600) {
            var sequence = peptide.Sequence;
            if (container.IsEmptyOrNull()) return new List<Peptide>() { Sequence2Peptide(peptide) };

            var currentModCount = peptide.CountModifiedAminoAcids();
            var results = new List<List<AminoAcid>>();
            EnumerateModifications(peptide, container, 0, currentModCount, maxNumberOfModificationsPerPeptide, new List<AminoAcid>(), results);

            var peptides = new List<Peptide>();
            foreach (var result in results) {
                var nPep = new Peptide() {
                    DatabaseOrigin = peptide.DatabaseOrigin, DatabaseOriginID = peptide.DatabaseOriginID, Sequence = peptide.Sequence,
                    Position = new Range(peptide.Position.Start, peptide.Position.End), IsProteinCterminal = peptide.IsProteinCterminal, IsProteinNterminal = peptide.IsProteinNterminal
                };
                nPep.SequenceObj = result;
                var formula = CalculatePeptideFormula(result);
                if (formula.Mass > maxPeptideMass) continue;
                nPep.ExactMass = formula.Mass;
                peptides.Add(nPep);
            }


            return peptides;
        }

        static void EnumerateModifications(Peptide pep, ModificationContainer container, int index, int numModifications, int maxModifications, 
            List<AminoAcid> aminoacids, List<List<AminoAcid>> result) {

            //Console.WriteLine(index);
            if (index >= pep.Sequence.Length) {
                result.Add(aminoacids.ToList());
                return;
            }
            var originAA = pep.SequenceObj[index];
            aminoacids.Add(originAA);
            EnumerateModifications(pep, container, index + 1, numModifications, maxModifications, aminoacids, result);
            aminoacids.RemoveAt(index);

            if (maxModifications > numModifications) {
                var mod = originAA.Modifications.IsEmptyOrNull() ? new List<Modification>() : originAA.Modifications.ToList();
                var modifiedAA = GetAminoAcidByVariableModifications(pep, mod, container, index);

                if (modifiedAA.IsModified() && originAA.Code() != modifiedAA.Code()) {
                    aminoacids.Add(modifiedAA);
                    EnumerateModifications(pep, container, index + 1, numModifications + 1, maxModifications, aminoacids, result);
                    aminoacids.RemoveAt(index);
                }
            }
        }

        public static AminoAcid GetAminoAcidByVariableModifications(Peptide peptide, List<Modification> modseq, ModificationContainer container, int index) {

            var isProteinNTerminal = peptide.IsProteinNterminal;
            var isProteinCTerminal = peptide.IsProteinCterminal;

            var sequence = peptide.Sequence;
            var aaChar = sequence[index];

            var isPeptideNTerminal = index == 0 ? true : false;
            var isPeptideCTerminal = index == sequence.Length - 1 ? true : false;

            return GetAminoAcidByVariableModifications(modseq, container, aaChar, isPeptideNTerminal, isPeptideCTerminal, isProteinNTerminal, isProteinCTerminal);
        }

        public static AminoAcid GetAminoAcidByVariableModifications(List<Modification> modseq, ModificationContainer container, 
            char aaChar, bool isPeptideNTerminal, bool isPeptideCTerminal, bool isProteinNTerminal, bool isProteinCTerminal) {
            if (isPeptideNTerminal && isProteinNTerminal && container.ProteinNterm2VariableMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.ProteinNterm2VariableMod[aaChar]);
            }
            else if (isPeptideNTerminal && container.AnyNtermSite2VariableMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.AnyNtermSite2VariableMod[aaChar]);
            }

            if (!isPeptideNTerminal && !isPeptideCTerminal && container.NotCtermSite2VariableMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.NotCtermSite2VariableMod[aaChar]);
            }

            if (isPeptideCTerminal && isProteinCTerminal && container.ProteinCtermSite2VariableMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.ProteinCtermSite2VariableMod[aaChar]);
            }
            else if (isPeptideCTerminal && container.AnyCtermSite2VariableMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.AnyCtermSite2VariableMod[aaChar]);
            }

            if (container.AnywehereSite2VariableMod[aaChar].IsModified()) {
                SetModificationSequence(modseq, container.AnywehereSite2VariableMod[aaChar]);
            }

            var compositions = ModificationUtility.GetModifiedCompositions(aaChar.ToString(), modseq);
            var code = compositions.Item1;
            if (!container.Code2AminoAcidObj.IsEmptyOrNull() && container.Code2AminoAcidObj.ContainsKey(code)) {
                return container.Code2AminoAcidObj[code]; 
            }
            else if (!container.Code2AminoAcidObj.IsEmptyOrNull() && code == string.Empty) {
                return container.Code2AminoAcidObj[aaChar.ToString()]; ;
            }
            else {
                var aa = new AminoAcid(container.AnywehereSite2VariableMod[aaChar].OriginalAA, compositions.Item1, compositions.Item2);
                return aa;
            }

        }

        public static Formula CalculatePeptideFormula(List<AminoAcid> aaSequence) {
            var dict = new Dictionary<string, int>();
            foreach (var aa in aaSequence) {
                var formula = aa.GetFormula();
                foreach (var pair in formula.Element2Count) {
                    if (dict.ContainsKey(pair.Key)) {
                        dict[pair.Key] += pair.Value;
                    }
                    else {
                        dict[pair.Key] = pair.Value;
                    }
                }
            }

            var offsetHydrogen = (aaSequence.Count - 1) * 2;
            var offsetOxygen = aaSequence.Count - 1;

            dict["H"] -= offsetHydrogen;
            dict["O"] -= offsetOxygen;

            return new Formula(dict);
        }

        public static void SetModificationSequence(List<Modification> modseq, ModificationProtocol protocol) {
            var mods = protocol.ModSequence;
            foreach (var mod in mods) {
                modseq.Add(mod);
            }
        }

        public static Formula Sequence2Formula(string sequence) {
            var carbon = 0;
            var hydrogen = 0;
            var nitrogen = 0;
            var oxygen = 0;
            var sulfur = 0;

            var char2formula = AminoAcidObjUtility.OneChar2Formula;
            var offsetHydrogen = (sequence.Length - 1) * 2;
            var offsetOxygen = sequence.Length - 1;

            for (int i = 0; i < sequence.Length; i++) {
                var aaChar = sequence[i];
                if (char2formula.ContainsKey(aaChar)) {
                    carbon += char2formula[aaChar].Cnum;
                    hydrogen += char2formula[aaChar].Hnum;
                    nitrogen += char2formula[aaChar].Nnum;
                    oxygen += char2formula[aaChar].Onum;
                    sulfur += char2formula[aaChar].Snum;
                }
            }

            return new Formula(carbon, hydrogen - offsetHydrogen, nitrogen, oxygen - offsetOxygen, 0, sulfur, 0, 0, 0, 0, 0);
        }
    }
}
