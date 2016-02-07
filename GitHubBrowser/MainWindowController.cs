using FileHelpers;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GitUserBrowser
{
    public class MainWindowController
    {
        private DateTime lastSearch;
        private const string GithubAppId = "GitUserBrowserTest";
        private MainWindowModel model;
        private IPasswordProvider passwordProvider;
        private CancellationTokenSource cts;

        private const int ApiRateTimeoutMin = 2;
        private const int MaxUsersPerPage = 20;

        private readonly int MaxVisibleUsers;

        private bool Hit1000Limit { get; set; }

        /// <summary>
        /// Public ctor
        /// </summary>
        /// <param name="m">The model to operate on</param>
        /// <param name="passProvider">Pass provider</param>
        /// <param name="usersPerPage">Number of visible user records per page</param>
        public MainWindowController(MainWindowModel m, IPasswordProvider passProvider, int usersPerPage)
        {
            model = m;
            passwordProvider = passProvider;
            cts = new CancellationTokenSource();
            MaxVisibleUsers = usersPerPage;
        }

        /// <summary>
        /// Local func for performing an exception-safe user search
        /// </summary>
        /// <param name="ct">cancellation token</param>
        /// <param name="search">search request</param>
        /// <returns></returns>
        private async Task<SearchUsersResult> SearchUsers(CancellationToken ct, SearchUsersRequest search)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                return await CreateGitClient().Search.SearchUsers(search);
            }
            catch(Exception e) when (e is OperationCanceledException || e is RateLimitExceededException || e is ApiValidationException)
            {
                cts.Cancel();
                Hit1000Limit = true;
                return await Task.FromResult(new SearchUsersResult(0, true, new List<User>()));
            }
        }

        /// <summary>
        /// Updates the main window status message
        /// </summary>
        private void RefreshStatusMessage()
        {
            var labelContent = string.Empty;

            if (Hit1000Limit)
                labelContent = string.Format("Results were trimmed to {0} by GitHub's API. Next search should be in at least {1} minutes", model.SearchResults.Count, ApiRateTimeoutMin);
            else
                labelContent = string.Format("Results found: {0}", model.SearchResults.Count);

            model.StatusMessage = labelContent;
        }

        /// <summary>
        /// Creates a new git client. Needed to avoid collisions when issuing parallel queries
        /// </summary>
        /// <returns>A git client</returns>
        private GitHubClient CreateGitClient()
        {
            if (string.IsNullOrEmpty(model.UserName) ||
                string.IsNullOrEmpty(passwordProvider.GetPassword()))
            {
                throw new ApplicationException("Invalid user details. Could not login in GitHub");
            }

            var client = new GitHubClient(new ProductHeaderValue(GithubAppId));
            client.Credentials = new Credentials(model.UserName, passwordProvider.GetPassword());
            return client;
        }

        /// <summary>
        /// Loads serially a page of users asynchronously. Only loads non-cached users
        /// </summary>
        /// <param name="beginIdx">Start idx of the first user to load</param>
        /// <param name="itemsCount">Total number of users to load</param>
        /// <returns></returns>
        public async Task LoadVisibleUserDataAsync(int beginIdx)
        {
            if (model.SearchResults.Count == 0
                || beginIdx > model.SearchResults.Count)
                return;

            int itemsCount = Math.Min(MaxVisibleUsers, model.SearchResults.Count - beginIdx);
            var usersToLoad = model.GetUsersToLoad(beginIdx, itemsCount);

            foreach (var user in usersToLoad)
            {
                Debug.WriteLine(string.Format("Loading user {0}", user.Item2.Login));
                var fullyLoadedUser = await CreateGitClient().User.Get(user.Item2.Login);
                model.AddUserToCache(user.Item1, fullyLoadedUser);
            }
        }

        /// <summary>
        /// Loads all user not-yet-cached user data in pages
        /// </summary>
        /// <returns></returns>
        public async Task LoadAllUserDataAsync()
        {
            if (model.SearchResults.Count == 0)
                return;

            List<Task<Dictionary<int, User>>> loadUsersTasks = new List<Task<Dictionary<int, User>>>();
            List<Tuple<int, User>> usersToLoad = model.GetUsersToLoad(0, model.SearchResults.Count) as List<Tuple<int, User>>;

            cts = new CancellationTokenSource();

            // determine workers count
            int workersCount = 1 + usersToLoad.Count / MaxUsersPerPage;
            Debug.WriteLine(string.Format("Identified {0} users to load on {1} threads", usersToLoad.Count, workersCount));

            // determine each worker's scope and kick them off
            // note: each worker operates on a separate thread
            for (int i = 0; i < workersCount; ++i)
            {
                int beginIdx = i * MaxUsersPerPage;
                int pageCount = Math.Min(MaxUsersPerPage, usersToLoad.Count - beginIdx);

                loadUsersTasks.Add(Task.Factory.StartNew(async () =>
                {
                    Dictionary<int, User> localUsers = new Dictionary<int, User>();

                    for(int usrBeginIdx = beginIdx; usrBeginIdx < beginIdx + pageCount; ++usrBeginIdx)
                    {
                        var currUser = usersToLoad[usrBeginIdx];
                        var fullyLoadedUser = await CreateGitClient().User.Get(currUser.Item2.Login).ConfigureAwait(false);
                        localUsers[currUser.Item1] = fullyLoadedUser;
                    }

                    return localUsers;

                }, cts.Token).Unwrap());
            }

            await Task.WhenAll(loadUsersTasks.ToArray());

            // populate the workers' results back in the model (done on the UI thread)
            foreach(var task in loadUsersTasks)
            {
                foreach (var loadedUser in task.Result)
                {
                    model.AddUserToCache(loadedUser.Key, loadedUser.Value);
                }
            }
        }

        /// <summary>
        /// Initiates a search based on the model's search criteria
        /// </summary>
        public async Task SearchAsync()
        {
            // validate if a search can be executed
            var minutesElapsed = TimeSpan.FromTicks(DateTime.Now.Ticks - lastSearch.Ticks).Minutes;
            if (Hit1000Limit && minutesElapsed < ApiRateTimeoutMin)
            {
                model.StatusMessage = string.Format("GitHub search rate exceeded. You need to wait for {0} more minutes", ApiRateTimeoutMin - minutesElapsed);
                return;
            }

            try
            {
                // cancel whatever else is going on and recreate cancellation source
                cts.Cancel();
                cts = new CancellationTokenSource();
                Hit1000Limit = false;
                model.SearchResults.Clear();
                model.StatusMessage = "Searching GitHub";

                // get all queries to execute
                var queries = model.GetQueryString();
                var res = new List<User>();

                // though the queries are serialized here, each of them is executed in a page-per-thread (the paginator takes care)
                foreach (var query in queries)
                {
                    var tmpRes = await new ApiResultPaginator<SearchUsersRequest, SearchUsersResult, User>(() => new SearchUsersRequest(query)).GetAllAsync((x, ct) => SearchUsers(ct, x), cts.Token);
                    res.AddRange(tmpRes);
                }

                model.SearchResults = new System.Collections.ObjectModel.ObservableCollection<User>(res);
                RefreshStatusMessage();
                lastSearch = DateTime.Now;
            }
            catch(Exception e)
            {
                cts.Cancel();
                model.StatusMessage = e.Message;
            }
        }

        /// <summary>
        /// Loads all not-yet-loaded user data and exports it to CSV
        /// </summary>
        /// <returns></returns>
        public async Task ExportResultsToCSVAsync()
        {
            if (model.SearchResults.Count == 0)
            {
                model.StatusMessage = "There are no results to export";
                return;
            }

            model.StatusMessage = "Fetching all results data from GitHub.. Please wait, it may take a few minutes";
            await this.LoadAllUserDataAsync();
            model.StatusMessage = "Data loadded";

            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.DefaultExt = ".csv";
            dlg.Filter = "(*.csv)|*.csv";

            if (true == dlg.ShowDialog())
            {
                string filename = dlg.FileName;

                try
                {
                    model.StatusMessage = "Persisting user data in: " + filename;
                    var engine = new FileHelperEngine<SerializableUser>();
                    engine.HeaderText = engine.GetFileHeader();
                    engine.WriteFile(filename, model.SerializableResults);
                    model.StatusMessage = "Data persisted in: " + filename;
                }
                catch
                {
                    model.StatusMessage = "Error persisting data in " + filename;
                }
            }
        }
    }
}
