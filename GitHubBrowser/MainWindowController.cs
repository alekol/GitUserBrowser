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
        private const string GithubAppId = "GitUserBrowserTest";
        private MainWindowModel model;
        private IPasswordProvider passwordProvider;
        private CancellationTokenSource cts;
        private GitHubClient client;

        private bool Hit1000Limit { get; set; }


        public MainWindowController(MainWindowModel m, IPasswordProvider passProvider)
        {
            model = m;
            passwordProvider = passProvider;
        }

        private async Task<SearchUsersResult> SearchUsers(GitHubClient client, CancellationToken ct, SearchUsersRequest search)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                Debug.WriteLine(string.Format("Obtaining page: {0}", search.Page));
                return await client.Search.SearchUsers(search);
            }
            catch(OperationCanceledException)
            {
                return await Task.FromResult(new SearchUsersResult(0, true, new List<User>()));
            }
            catch(ApiValidationException)
            {
                cts.Cancel();
                Hit1000Limit = true;
                return await Task.FromResult(new SearchUsersResult(0, true, new List<User>()));
            }
        }

        public void UpdateSelectedItems()
        {
            model.StatusMessage = string.Format("{0}:  Just scrolled", DateTime.Now);
        }

        private void RefreshCount()
        {
            var labelContent = string.Empty;

            if (Hit1000Limit)
                labelContent += "Results were trimmed to 1000 by GitHub's API";
            else
                labelContent = string.Format("Results found: {0}", model.SearchResults.Count);

            model.StatusMessage = labelContent;
        }

        private void InitializeGitClient()
        {
            if (string.IsNullOrEmpty(model.UserName) ||
                string.IsNullOrEmpty(passwordProvider.GetPassword()))
            {
                throw new ApplicationException("Invalid user details. Could not login in GitHub");
            }

            client = new GitHubClient(new ProductHeaderValue(GithubAppId));
            client.Credentials = new Credentials(model.UserName, passwordProvider.GetPassword());
        }

        public async Task LoadItemsFull(int beginIdx, int itemsCount)
        {
            if (itemsCount == 0
                || itemsCount > model.SearchResults.Count
                || beginIdx > model.SearchResults.Count)
                return;

            var usersToLoad = model.GetUsersToLoad(beginIdx, itemsCount);

            foreach (var user in usersToLoad)
            {
                Debug.WriteLine(string.Format("Loading user {0}", user.Item2.Login));
                var fullyLoadedUser = await client.User.Get(user.Item2.Login);
                model.UpdateUser(fullyLoadedUser, user.Item1);
            }
        }

        public async void Search()
        {
            try
            {
                model.SearchResults.Clear();
                model.StatusMessage = "Searching GitHub";

                Hit1000Limit = false;
                cts = new CancellationTokenSource();

                InitializeGitClient();
                var queries = model.GetQueryString();
                var res = new List<User>();

                foreach (var query in queries)
                {
                    var apiRequest = new SearchUsersRequest(query);
                    var tmpRes = await new ApiResultPaginator<SearchUsersRequest, SearchUsersResult, User>(apiRequest).GetAll((x, ct) => SearchUsers(client, ct, x), cts.Token);
                    res.AddRange(tmpRes);
                }

                model.SearchResults = new System.Collections.ObjectModel.ObservableCollection<User>(res);
                RefreshCount();

                LoadItemsFull(0, Math.Min(30, res.Count));
            }
            catch(Exception e)
            {
                cts.Cancel();
                model.StatusMessage = e.Message;
            }
        }

        public async Task ExportResultsToCSV()
        {
            if (model.SearchResults.Count == 0)
            {
                model.StatusMessage = "There are no results to export";
                return;
            }

            model.StatusMessage = "Fetching all results data from GitHub.. Please wait";

            await this.LoadItemsFull(0, model.SearchResults.Count);

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
                catch(Exception e)
                {
                    model.StatusMessage = "Error persisting data in " + filename;
                }
            }
        }
    }
}
