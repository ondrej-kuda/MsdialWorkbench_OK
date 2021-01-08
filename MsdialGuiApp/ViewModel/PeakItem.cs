﻿using CompMs.Common.Interfaces;

namespace CompMs.App.Msdial.ViewModel
{
    public class PeakItem
    {
        public PeakItem(IChromatogramPeak chrom) {
            this.chrom = chrom;
        }
        private readonly IChromatogramPeak chrom;

        public double Intensity => chrom.Intensity;
        public double Time => chrom.ChromXs.Value;
    }
}
