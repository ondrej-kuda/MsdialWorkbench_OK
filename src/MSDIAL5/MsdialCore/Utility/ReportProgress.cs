﻿using System;

namespace CompMs.MsdialCore.Utility {
    public sealed class ReportProgress {
        private readonly double _initialProgress;
        private readonly double _progressMax;
        private readonly Action<int> _reportAction;
        private readonly object _syncObject;

        public ReportProgress(double initialProgress, double progressMax, Action<int> reportAction) {
            _initialProgress = initialProgress;
            _progressMax = progressMax;
            _reportAction = reportAction;
            _syncObject = new object();
        }

        public double InitialProgress => _initialProgress;
        public double ProgressMax => _progressMax;
        public Action<int> ReportAction => _reportAction;

        public void Show(double current, double localMax) {
            lock (_syncObject) {
                Show(_initialProgress, _progressMax, current, localMax, _reportAction);
            }
        }

        public static void Show(double initial, double totalMax, double current, double localMax, Action<int> reportAction) {
            if (localMax == 0) return;
            var progress = initial + current / localMax * totalMax;
            reportAction?.Invoke(((int)progress));
        }

        public static ReportProgress FromLength(Action<int> reportAction, double initialProgress, double progressLength) {
            return new ReportProgress(initialProgress, progressLength, reportAction);
        }

        public static ReportProgress FromRange(Action<int> reportAction, double initialProgress, double endProgress) {
            return new ReportProgress(initialProgress, endProgress - initialProgress, reportAction);
        }

        public static ReportProgress FromLength(Action<double> reportAction, double initialProgress, double progressLength) {
            return new ReportProgress(initialProgress, progressLength, v => reportAction?.Invoke(v));
        }

        public static ReportProgress FromRange(Action<double> reportAction, double initialProgress, double endProgress) {
            return new ReportProgress(initialProgress, endProgress - initialProgress, v => reportAction?.Invoke(v));
        }
    }

    public static class ReportProgressExtensions {
        public static ReportProgress FromLength(this Action<int> reportAction, double initialProgress, double progressLength) {
            return new ReportProgress(initialProgress, progressLength, reportAction);
        }

        public static ReportProgress FromRange(this Action<int> reportAction, double initialProgress, double endProgress) {
            return new ReportProgress(initialProgress, endProgress - initialProgress, reportAction);
        }

        public static ReportProgress FromLength(this Action<double> reportAction, double initialProgress, double progressLength) {
            return new ReportProgress(initialProgress, progressLength, v => reportAction?.Invoke(v));
        }

        public static ReportProgress FromRange(this Action<double> reportAction, double initialProgress, double endProgress) {
            return new ReportProgress(initialProgress, endProgress - initialProgress, v => reportAction?.Invoke(v));
        }
    }
}
