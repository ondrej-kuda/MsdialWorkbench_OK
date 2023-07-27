﻿using CompMs.Common.DataStructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public interface ITotalChain : IEquatable<ITotalChain>, IVisitableElement
    {
        int CarbonCount { get; }
        int DoubleBondCount { get; }
        int OxidizedCount { get; }
        int ChainCount { get; }
        int AcylChainCount { get; }
        int AlkylChainCount { get; }
        int SphingoChainCount { get; }
        double Mass { get; }
        LipidDescription Description { get; }

        /// <summary>
        /// </summary>
        /// <param name="snPosition">1-indexed sn position</param>
        /// <returns></returns>
        IChain GetChain(int snPosition);
        IChain[] GetAllChains();
        bool Includes(ITotalChain chains);
        IEnumerable<ITotalChain> GetCandidateSets(ITotalChainVariationGenerator totalChainGenerator);
    }

    public static class TotalChainExtension
    {
        public static ITotalChain GetChains(this LipidMolecule lipid) {
            var prop = LipidClassDictionary.Default.LbmItems[lipid.LipidClass];
            switch (lipid.AnnotationLevel) {
                case 1:
                    return new TotalChain(lipid.TotalCarbonCount, lipid.TotalDoubleBondCount, lipid.TotalOxidizedCount, acylChainCount: prop.AcylChain, alkylChainCount: prop.AlkylChain, sphingoChainCount: prop.SphingoChain);
                case 2:
                case 3:
                    return new MolecularSpeciesLevelChains(GetEachChains(lipid, prop));
                default:
                    break;
            }
            return default;
        }

        private static IChain[] GetEachChains(LipidMolecule lipid, LipidClassProperty prop) {
            var chains = new IChain[prop.TotalChain];
            if (prop.TotalChain >= 1) {
                if (prop.SphingoChain >= 1) {
                    chains[0] = new SphingoChain(lipid.Sn1CarbonCount, new DoubleBond(lipid.Sn1DoubleBondCount), new Oxidized(lipid.Sn1Oxidizedount));
                    // chains[0] = SphingoParser.Parse(lipid.Sn1AcylChainString);
                }
                else if (prop.AlkylChain >= 1) {
                    chains[0] = new AlkylChain(lipid.Sn1CarbonCount, new DoubleBond(lipid.Sn1DoubleBondCount), new Oxidized(lipid.Sn1Oxidizedount));
                }
                else {
                    chains[0] = new AcylChain(lipid.Sn1CarbonCount, new DoubleBond(lipid.Sn1DoubleBondCount), new Oxidized(lipid.Sn1Oxidizedount));
                }
            }
            if (prop.TotalChain >= 2) {
                chains[1] = new AcylChain(lipid.Sn2CarbonCount, new DoubleBond(lipid.Sn2DoubleBondCount), new Oxidized(lipid.Sn2Oxidizedount));
            }
            if (prop.TotalChain >= 3) {
                chains[2] = new AcylChain(lipid.Sn3CarbonCount, new DoubleBond(lipid.Sn3DoubleBondCount), new Oxidized(lipid.Sn3Oxidizedount));
            }
            if (prop.TotalChain >= 4) {
                chains[3] = new AcylChain(lipid.Sn4CarbonCount, new DoubleBond(lipid.Sn4DoubleBondCount), new Oxidized(lipid.Sn4Oxidizedount));
            }
            return chains;
        }

        public static IEnumerable<T> GetTypedChains<T>(this ITotalChain chain) where T : IChain {
            return chain.GetAllChains().OfType<T>();
        }

        public static (T, U) Deconstruct<T, U>(this ITotalChain chain) where T : IChain where U : IChain {
            if (chain.ChainCount != 2 || typeof(T) == typeof(U)) {
                return default;
            }
            var t = chain.GetTypedChains<T>().SingleOrDefault();
            var u = chain.GetTypedChains<U>().SingleOrDefault();
            if (t is T && u is U) {
                return (t, u);
            }
            return default;
        }

        public static void ApplyToChain(this ITotalChain chains, int snPosition, Action<IChain> action) {
            var chain = chains.GetChain(snPosition);
            if (chain != null) {
                action?.Invoke(chain);
            }
        }

        public static void ApplyToChain<T>(this ITotalChain chains, int snPosition, Action<T> action) where T: IChain {
            var chain = chains.GetChain(snPosition);
            if (chain is T c) {
                action?.Invoke(c);
            }
        }
    }
}
