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
    public class ApiResultPaginator<TApiSearch, TApiRes, TRes>
        where TApiSearch : BaseSearchRequest
        where TApiRes : SearchResult<TRes>
    {
        TApiSearch searchRequest;
        private const int DefaultPageSize = 100;

        public ApiResultPaginator(TApiSearch request)
        {
            searchRequest = request;
        }

        public async Task<IEnumerable<TRes>> GetAll(Func<TApiSearch, CancellationToken, Task<TApiRes>> searchFunc, CancellationToken ct) 
        {
            if (searchFunc == null)
                throw new ArgumentNullException("searchFunc");

            var firstPage = await searchFunc(searchRequest, ct).ConfigureAwait(false);
            Debug.WriteLine(string.Format("{0} items in total, {1} in current page, thread {2}", firstPage.TotalCount, firstPage.Items.Count, Thread.CurrentThread.ManagedThreadId));

            if (firstPage.TotalCount <= firstPage.Items.Count)
                return firstPage.Items;

            List<TRes> results = new List<TRes>(firstPage.Items);

            for (int i = 2; i <= (firstPage.TotalCount / DefaultPageSize) + 1; ++i)
            {
                if (ct.IsCancellationRequested)
                    break;

                searchRequest.Page = i;
                var newPage = await searchFunc(searchRequest, ct).ConfigureAwait(false);
                Debug.WriteLine(string.Format("Done obtaining page {0} with {1} results on thread {2}", searchRequest.Page, newPage.Items.Count, Thread.CurrentThread.ManagedThreadId));

                lock (results)
                {
                    results.AddRange(newPage.Items);
                }
            }

            return results;
        } 
    }
}
