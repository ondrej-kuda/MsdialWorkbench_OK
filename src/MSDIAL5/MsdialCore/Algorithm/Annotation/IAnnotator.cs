﻿using System.Collections.Generic;

namespace CompMs.MsdialCore.Algorithm.Annotation
{
    public interface IMatchResultFinder<in TQuery, TResult>
    {
        string Id { get; }
        List<TResult> FindCandidates(TQuery query);
        int Priority { get; }
    }

    public interface IAnnotator<in TQuery, out TReference, TResult> : IMatchResultFinder<TQuery, TResult>, IMatchResultRefer<TReference, TResult>, IMatchResultEvaluator<TResult>
    {
    }

    public interface ISerializableAnnotator<in TQuery, TReference, TResult, in TDatabase>
        : IAnnotator<TQuery, TReference, TResult>, IRestorableRefer<TQuery, TReference, TResult, TDatabase>
    {

    }
}
