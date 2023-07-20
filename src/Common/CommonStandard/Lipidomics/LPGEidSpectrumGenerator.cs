﻿using CompMs.Common.Components;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.FormulaGenerator.DataObj;
using CompMs.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public class LPGEidSpectrumGenerator : ILipidSpectrumGenerator
    {

        private static readonly double C3H9O6P = new[]
        {
            MassDiffDictionary.CarbonMass * 3,
            MassDiffDictionary.HydrogenMass * 9,
            MassDiffDictionary.OxygenMass * 6,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double C3H6O2 = new[]
        {
            MassDiffDictionary.CarbonMass * 3,
            MassDiffDictionary.HydrogenMass * 6,
            MassDiffDictionary.OxygenMass * 2,
        }.Sum();

        private static readonly double Gly_C = new[] {
            MassDiffDictionary.CarbonMass * 6,
            MassDiffDictionary.HydrogenMass * 13,
            MassDiffDictionary.OxygenMass * 6,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double Gly_O = new[] {
            MassDiffDictionary.CarbonMass * 5,
            MassDiffDictionary.HydrogenMass * 11,
            MassDiffDictionary.OxygenMass * 7,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double CH2 = new[]
        {
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.CarbonMass,
        }.Sum();

        private static readonly double H2O = new[]
        {
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.OxygenMass,
        }.Sum();

        public LPGEidSpectrumGenerator()
        {
            spectrumGenerator = new SpectrumPeakGenerator();
        }

        public LPGEidSpectrumGenerator(ISpectrumPeakGenerator peakGenerator)
        {
            this.spectrumGenerator = peakGenerator ?? throw new System.ArgumentNullException(nameof(peakGenerator));
        }

        public bool CanGenerate(ILipid lipid, AdductIon adduct)
        {
            if (lipid.LipidClass == LbmClass.LPG)
            {
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+NH4]+")
                {
                    return true;
                }
            }
            return false;
        }

        public IMSScanProperty Generate(Lipid lipid, AdductIon adduct, IMoleculeProperty molecule = null)
        {
            var spectrum = new List<SpectrumPeak>();
            var nlMass = adduct.AdductIonName == "[M+NH4]+" ? adduct.AdductIonAccurateMass + H2O : H2O;
            spectrum.AddRange(GetLPGSpectrum(lipid, adduct));
            if (lipid.Chains is MolecularSpeciesLevelChains mlChains)
            {
                spectrum.AddRange(GetAcylLevelSpectrum(lipid, mlChains.GetChains(), adduct));
                spectrum.AddRange(GetAcylDoubleBondSpectrum(lipid, mlChains.GetChains().OfType<AcylChain>(), adduct));
                spectrum.AddRange(EidSpecificSpectrum(lipid, adduct, nlMass, 150d));
            }
            if (lipid.Chains is PositionLevelChains plChains)
            {
                spectrum.AddRange(GetAcylLevelSpectrum(lipid, plChains.GetChains(), adduct));
                //spectrum.AddRange(GetAcylPositionSpectrum(lipid, plChains.Chains[0], adduct));
                spectrum.AddRange(GetAcylDoubleBondSpectrum(lipid, plChains.GetChains().OfType<AcylChain>(), adduct));
                spectrum.AddRange(EidSpecificSpectrum(lipid, adduct, nlMass, 150d));
            }
            spectrum = spectrum.GroupBy(spec => spec, comparer)
                .Select(specs => new SpectrumPeak(specs.First().Mass, specs.Sum(n => n.Intensity), string.Join(", ", specs.Select(spec => spec.Comment)), specs.Aggregate(SpectrumComment.none, (a, b) => a | b.SpectrumComment)))
                .OrderBy(peak => peak.Mass)
                .ToList();
            return CreateReference(lipid, adduct, spectrum, molecule);
        }

        private MoleculeMsReference CreateReference(ILipid lipid, AdductIon adduct, List<SpectrumPeak> spectrum, IMoleculeProperty molecule)
        {
            return new MoleculeMsReference
            {
                PrecursorMz = adduct.ConvertToMz(lipid.Mass),
                IonMode = adduct.IonMode,
                Spectrum = spectrum,
                Name = lipid.Name,
                Formula = molecule?.Formula,
                Ontology = molecule?.Ontology,
                SMILES = molecule?.SMILES,
                InChIKey = molecule?.InChIKey,
                AdductType = adduct,
                CompoundClass = lipid.LipidClass.ToString(),
                Charge = adduct.ChargeNumber,
            };
        }

        private SpectrumPeak[] GetLPGSpectrum(ILipid lipid, AdductIon adduct)
        {
            var adductmass = adduct.AdductIonName == "[M+NH4]+" ? MassDiffDictionary.ProtonMass : adduct.AdductIonAccurateMass;
            var lipidMass = lipid.Mass + adductmass;

            var spectrum = new List<SpectrumPeak>();
            if (adduct.AdductIonName == "[M+H]+")
            {
                spectrum.AddRange(
                    new[]
                    {
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass), 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                    }
                );
            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                spectrum.AddRange(
                    new[]
                    {
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass), 500d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                        new SpectrumPeak(lipidMass, 999d, "[M+H]+ "){ SpectrumComment = SpectrumComment.metaboliteclass },
                    }
                );
            }
            if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+NH4]+")
            {
                spectrum.AddRange(
                    new[]
                    {
                        new SpectrumPeak(lipidMass - C3H6O2, 100d, "Precursor -C3H6O2") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(lipidMass - H2O, 500d, "[M+H]+ -H2O"){ SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(lipidMass - H2O * 2, 200d, "[M+H]+ -2H2O"){ SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(lipidMass - C3H6O2 - H2O, 100d, "[M+H]+ -C3H6O2 -H2O") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(Gly_C + MassDiffDictionary.ProtonMass, 100d, "Gly-C") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(Gly_O + MassDiffDictionary.ProtonMass, 100d, "Gly-O") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(C3H9O6P+ MassDiffDictionary.ProtonMass, 300d, "Header") { SpectrumComment = SpectrumComment.metaboliteclass, IsAbsolutelyRequiredFragmentForAnnotation = true },
                        new SpectrumPeak(C3H9O6P - H2O+ MassDiffDictionary.ProtonMass, 200d, "Header - H2O") { SpectrumComment = SpectrumComment.metaboliteclass, IsAbsolutelyRequiredFragmentForAnnotation = true },
                        new SpectrumPeak(lipid.Mass - C3H9O6P+ MassDiffDictionary.ProtonMass, 500d, "Precursor -C3H9O6P") { SpectrumComment = SpectrumComment.metaboliteclass },
                    }
                );
            }
            return spectrum.ToArray();
        }

        private IEnumerable<SpectrumPeak> GetAcylDoubleBondSpectrum(ILipid lipid, IEnumerable<AcylChain> acylChains, AdductIon adduct)
        {
            var nlMass = adduct.AdductIonName == "[M+NH4]+" ? adduct.AdductIonAccurateMass + H2O : H2O;
            return acylChains.SelectMany(acylChain => spectrumGenerator.GetAcylDoubleBondSpectrum(lipid, acylChain, adduct, nlMass, 20d));
        }

        private IEnumerable<SpectrumPeak> GetAcylLevelSpectrum(ILipid lipid, IEnumerable<IChain> acylChains, AdductIon adduct)
        {
            return acylChains.SelectMany(acylChain => GetAcylLevelSpectrum(lipid, acylChain, adduct));
        }

        private SpectrumPeak[] GetAcylLevelSpectrum(ILipid lipid, IChain acylChain, AdductIon adduct)
        {
            var chainMass = acylChain.Mass - MassDiffDictionary.HydrogenMass;
            var adductMass = adduct.AdductIonName == "[M+NH4]+" ? MassDiffDictionary.ProtonMass : adduct.AdductIonAccurateMass;
            var spectrum = new List<SpectrumPeak>();
            if (chainMass != 0.0)
            {
                spectrum.AddRange
                (
                     new[]
                     {
                         new SpectrumPeak(lipid.Mass - chainMass + adductMass, 200d, $"-{acylChain}") { SpectrumComment = SpectrumComment.acylchain },
                         new SpectrumPeak(chainMass + MassDiffDictionary.ProtonMass, 100d, $"{acylChain} acyl") { SpectrumComment = SpectrumComment.acylchain },
                         //new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - H2O), 100d, $"-{acylChain}-H2O") { SpectrumComment = SpectrumComment.acylchain },
                     }
                );
            }
            return spectrum.ToArray();
        }

        private SpectrumPeak[] GetAcylPositionSpectrum(ILipid lipid, IChain acylChain, AdductIon adduct)
        {
            var chainMass = acylChain.Mass;
            return new[]
            {
                new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - MassDiffDictionary.OxygenMass - CH2), 100d, "-CH2(Sn1)") { SpectrumComment = SpectrumComment.snposition },
                new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - H2O - MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass - CH2), 100d, "-H2O -CH2(Sn1)") { SpectrumComment = SpectrumComment.snposition },
            };
        }

        private static SpectrumPeak[] EidSpecificSpectrum(Lipid lipid, AdductIon adduct, double nlMass, double intensity)
        {
            var spectrum = new List<SpectrumPeak>();
            if (lipid.Chains is SeparatedChains chains)
            {
                foreach (var chain in chains.GetChains())
                {
                    if (chain.DoubleBond.Count == 0 || chain.DoubleBond.UnDecidedCount > 0) continue;
                    spectrum.AddRange(EidSpecificSpectrumGenerator.EidSpecificSpectrumGen(lipid, chain, adduct, nlMass, intensity));
                }
            }
            return spectrum.ToArray();
        }

        private static readonly IEqualityComparer<SpectrumPeak> comparer = new SpectrumEqualityComparer();
        private readonly ISpectrumPeakGenerator spectrumGenerator;
    }
}
