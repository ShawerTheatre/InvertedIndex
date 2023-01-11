using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;

namespace InvertedIndexApp;

public class IndexService : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _isIndexed = false;

    private readonly ConcurrentDictionary<string, ImmutableHashSet<string>> _index = new();

    public bool IsIndexed
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _isIndexed;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
    
    private void SetIndexed()
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (!_isIndexed)
            {
                _lock.EnterWriteLock();
                try
                {
                    _isIndexed = true;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public string[]? Query(string word)
    {
        if (!IsIndexed)
        {
            throw new Exception("Files were not indexed yet");
        }
        
        var normalizedWord = NormalizeWord(word);
        if (_index.TryGetValue(normalizedWord, out var result))
        {
            return result.ToArray();
        }

        return null;
    }

    public double Index(int threadsCount = 1)
    {
        if (threadsCount <= 0) throw new ArgumentException("ThreadsCount must be greater than 0", nameof(threadsCount));
        if (threadsCount > Environment.ProcessorCount * 8) throw new ArgumentException("ThreadsCount is too large", nameof(threadsCount));

        var files = Directory.GetFiles("Input", "*.*", SearchOption.AllDirectories);

        _index.Clear();
        
        var sw = new Stopwatch();
        sw.Start();

        if (threadsCount == 1)
        {
            IndexSingleThreaded(files);
        }
        else
        {
            IndexMultiThreaded(files, threadsCount);
        }
        
        sw.Stop();

        SetIndexed();
        return sw.ElapsedMilliseconds;
    }

    private void IndexMultiThreaded(string[] files, int threadsCount)
    {
        var segmentLength = files.Length / threadsCount;
        var threads = new Thread[threadsCount];

        for (int i = 0; i < threads.Length; ++i)
        {
            var threadIndex = i;
            threads[i] = new Thread(() => {
                var currentSegmentStart = segmentLength * threadIndex;
                var currentSegmentLength = threadIndex + 1 == threads.Length
                    ? files.Length - currentSegmentStart
                    : segmentLength;
                
                var segment = files.AsSpan(currentSegmentStart, currentSegmentLength);
                IndexFiles(segment);
            });

            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }
    }

    private void IndexSingleThreaded(string[] files)
    {
        IndexFiles(files);
    }
    
    private void IndexFiles(Span<string> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            IndexFile(filePath);
        }
    }

    private void IndexFile(string filePath)
    {
        var words = File.ReadLines(filePath)
            .SelectMany(line => line.Split(' '))
            .Select(NormalizeWord);
        var resultPath = string.Join(Path.DirectorySeparatorChar, filePath.Split(Path.DirectorySeparatorChar).Skip(1));

        foreach (var word in words)
        {
            _index.AddOrUpdate(
            word,
            (_) => ImmutableHashSet.Create<string>(resultPath),
            (_, set) => set.Add(resultPath));
        }
    }

    private string NormalizeWord(string word)
    {
        return word.Trim().ToLowerInvariant();
    }
    
    public void Dispose()
    {
        _lock.Dispose();
    }
}
