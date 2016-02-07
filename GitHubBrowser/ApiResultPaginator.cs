using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GitUserBrowser
{
    /// <summary>
    /// An object that based on a number of pages the Git api returns, performs 
    /// a parallel fetching of all pages and assembles the data to return
    /// </summary>
    /// <typeparam name="TApiSearch">The type of the API search request</typeparam>
    /// <typeparam name="TApiRes">The type of the API search result</typeparam>
    /// <typeparam name="TRes">The type of the actual result object</typeparam>
    public class ApiResultPaginator<TApiSearch, TApiRes, TRes>
        where TApiSearch : BaseSearchRequest
        where TApiRes : SearchResult<TRes>
    {
        private Func<TApiSearch> createSearchRequest;
        public const int DefaultPageSize = 100;

        /// <summary>
        /// public ctor
        /// </summary>
        /// <param name="requestProvider">
        /// A Func<> that creates a new search request each time. 
        /// New instance is needed by the each of the parallel tasks to avoid closure-related issues
        /// </param>
        public ApiResultPaginator(Func<TApiSearch> requestProvider)
        {
            createSearchRequest = requestProvider;
        }

        /// <summary>
        /// Performs a parallel fetching of all pages provided by the search API 
        /// </summary>
        /// <param name="searchFunc">An exception-safe func that is used for executing the actual search</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>An enumerable of actual return objects</returns>
        public async Task<IEnumerable<TRes>> GetAllAsync(Func<TApiSearch, CancellationToken, Task<TApiRes>> searchFunc, CancellationToken ct) 
        {
            if (searchFunc == null)
                throw new ArgumentNullException("searchFunc");

            var firstPage = await searchFunc(createSearchRequest(), ct).ConfigureAwait(false);
            Debug.WriteLine(string.Format("{0} items in total, {1} in current page, thread {2}", firstPage.TotalCount, firstPage.Items.Count, Thread.CurrentThread.ManagedThreadId));

            if (firstPage.TotalCount <= firstPage.Items.Count)
                return firstPage.Items;

            List<TRes> results = new List<TRes>(firstPage.Items);
            List<Task> tasks = new List<Task>();

            for (int i = 2; i <= (firstPage.TotalCount / DefaultPageSize) + 1; ++i)
            {
                if (ct.IsCancellationRequested)
                    break;

                var newSearchRequest = createSearchRequest();
                newSearchRequest.Page = i;

                tasks.Add(Task.Factory.StartNew(async () =>
                    {
                        var newPage = await searchFunc(newSearchRequest, ct);
                        lock (results)
                        {
                            results.AddRange(newPage.Items);
                        }
                    }, ct).Unwrap());
            }

            await Task.WhenAll(tasks.ToArray());

            return results;
        } 
    }
}
