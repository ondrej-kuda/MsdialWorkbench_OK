﻿using CompMs.Common.Components;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.FormulaGenerator.DataObj;
using CompMs.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public class PESpectrumGenerator : ILipidSpectrumGenerator
    {

        private static readonly double C2H8NO4P = new[]
        {
            MassDiffDictionary.CarbonMass * 2,
            MassDiffDictionary.HydrogenMass * 8,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 4,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double CH5N = new[]
        {
            MassDiffDictionary.CarbonMass * 1,
            MassDiffDictionary.HydrogenMass * 5,
            MassDiffDictionary.NitrogenMass,
        }.Sum();

        private static readonly double Gly_C = new[] {
            MassDiffDictionary.CarbonMass * 5,
            MassDiffDictionary.HydrogenMass * 12,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 4,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double Gly_O = new[] {
            MassDiffDictionary.CarbonMass * 4,
            MassDiffDictionary.HydrogenMass * 10,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 5,
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

        public PESpectrumGenerator()
        {
            spectrumGenerator = new SpectrumPeakGenerator();
        }

        public PESpectrumGenerator(ISpectrumPeakGenerator spectrumGenerator)
        {
            this.spectrumGenerator = spectrumGenerator ?? throw new ArgumentNullException(nameof(spectrumGenerator));
        }

        private readonly ISpectrumPeakGenerator spectrumGenerator;

        public bool CanGenerate(ILipid lipid, AdductIon adduct)
        {
            if (lipid.LipidClass == LbmClass.PE)
            {
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+Na]+")
                {
                    return true;
                }
            }
            return false;
        }

        public IMSScanProperty Generate(Lipid lipid, AdductIon adduct, IMoleculeProperty molecule = null)
        {
            var spectrum = new List<SpectrumPeak>();
            spectrum.AddRange(GetPESpectrum(lipid, adduct));
            if (lipid.Chains is MolecularSpeciesLevelChains mlChains)
            {
                spectrum.AddRange(GetAcylLevelSpectrum(lipid, mlChains.Chains, adduct));
                spectrum.AddRange(GetAcylDoubleBondSpectrum(lipid, mlChains.Chains.OfType<AcylChain>(), adduct));
                spectrum.AddRange(GetAcylDoubleBondSpectrum(lipid, mlChains.Chains.OfType<AcylChain>(), adduct, nlMass: C2H8NO4P));
            }
            if (lipid.Chains is PositionLevelChains plChains)
            {
                spectrum.AddRange(GetAcylLevelSpectrum(lipid, plChains.Chains, adduct));
                spectrum.AddRange(GetAcylPositionSpectrum(lipid, plChains.Chains[0], adduct));
                spectrum.AddRange(GetAcylDoubleBondSpectrum(lipid, plChains.Chains.OfType<AcylChain>(), adduct));
                spectrum.AddRange(GetAcylDoubleBondSpectrum(lipid, plChains.Chains.OfType<AcylChain>(), adduct, nlMass: C2H8NO4P));
            }
            spectrum = spectrum.GroupBy(spec => spec, comparer)
                .Select(specs => new SpectrumPeak(specs.First().Mass, specs.Sum(n => n.Intensity), string.Join(", ", specs.Select(spec => spec.Comment))) { SpectrumComment = SpectrumComment.doublebond })
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

        private SpectrumPeak[] GetPESpectrum(ILipid lipid, AdductIon adduct)
        {
            var spectrum = new List<SpectrumPeak>
            {
                new SpectrumPeak(adduct.ConvertToMz(lipid.Mass), 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - C2H8NO4P), 500d, "Precursor -C2H8NO4P") { SpectrumComment = SpectrumComment.metaboliteclass },
                new SpectrumPeak(adduct.ConvertToMz(C2H8NO4P), 100d, "Header") { SpectrumComment = SpectrumComment.metaboliteclass },
                new SpectrumPeak(adduct.ConvertToMz(Gly_C), 100d, "Gly-C")  { SpectrumComment = SpectrumComment.metaboliteclass },
                new SpectrumPeak(adduct.ConvertToMz(Gly_O), 100d, "Gly-O") { SpectrumComment = SpectrumComment.metaboliteclass },
            };
            if (adduct.AdductIonName == "[M+Na]+")
            {
                spectrum.AddRange
                (
                     new[]
                     {
                        new SpectrumPeak(lipid.Mass - C2H8NO4P + MassDiffDictionary.ProtonMass, 250d, "Precursor -C2H8NO4P") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - CH5N), 200d, "Precursor -CH5N") { SpectrumComment = SpectrumComment.metaboliteclass },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - CH5N - CH2), 500d, "Precursor -CH5N -CH2") { SpectrumComment = SpectrumComment.metaboliteclass }
                     }
                );
            }

            return spectrum.ToArray();
        }

        private IEnumerable<SpectrumPeak> GetAcylDoubleBondSpectrum(ILipid lipid, IEnumerable<AcylChain> acylChains, AdductIon adduct, double nlMass = 0.0)
        {
            return acylChains.SelectMany(acylChain => spectrumGenerator.GetAcylDoubleBondSpectrum(lipid, acylChain, adduct, nlMass, 50d));
        }

        private IEnumerable<SpectrumPeak> GetAcylLevelSpectrum(ILipid lipid, IEnumerable<IChain> acylChains, AdductIon adduct)
        {
            return acylChains.SelectMany(acylChain => GetAcylLevelSpectrum(lipid, acylChain, adduct));
        }

        private SpectrumPeak[] GetAcylLevelSpectrum(ILipid lipid, IChain acylChain, AdductIon adduct)
        {
            var chainMass = acylChain.Mass - MassDiffDictionary.HydrogenMass;
            var spectrum = new List<SpectrumPeak>();
            if (adduct.AdductIonName == "[M+H]+")
            {
                spectrum.AddRange
                (
                    new[]
                    {
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass), 100d, $"-{acylChain}") { SpectrumComment = SpectrumComment.acylchain },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - C2H8NO4P), 100d, $"-Header -{acylChain}"){ SpectrumComment = SpectrumComment.acylchain },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass), 100d, $"-{acylChain}-O") { SpectrumComment = SpectrumComment.acylchain },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - C2H8NO4P - H2O), 100d, $"-Header -{acylChain}-O"){ SpectrumComment = SpectrumComment.acylchain },
                    }
                );
            }
            else if (adduct.AdductIonName == "[M+Na]+")
            {
                spectrum.AddRange
                (
                     new[]
                     {
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - MassDiffDictionary.HydrogenMass*2), 100d, $"-{acylChain}") { SpectrumComment = SpectrumComment.acylchain },
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - H2O), 100d, $"-{acylChain}-O") { SpectrumComment = SpectrumComment.acylchain },
                     }
                );
            }
            return spectrum.ToArray();
        }

        private SpectrumPeak[] GetAcylPositionSpectrum(ILipid lipid, IChain acylChain, AdductIon adduct)
        {
            var chainMass = acylChain.Mass;
            var spectrum = new List<SpectrumPeak>
            {
                new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - MassDiffDictionary.OxygenMass - CH2), 100d, "-CH2(Sn1)") { SpectrumComment = SpectrumComment.snposition },
            };
            if (adduct.AdductIonName == "[M+H]+")
            {
                spectrum.AddRange
                (
                     new[]
                     {
                        new SpectrumPeak(adduct.ConvertToMz(lipid.Mass - chainMass - C2H8NO4P - MassDiffDictionary.OxygenMass - CH2), 100d, "-Header -CH2(Sn1)") { SpectrumComment = SpectrumComment.snposition },
                     }
                );
            }
            return spectrum.ToArray();
        }


        private static readonly IEqualityComparer<SpectrumPeak> comparer = new SpectrumEqualityComparer();
    }
}
