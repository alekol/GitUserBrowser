using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitUserBrowser
{
    /// <summary>
    /// Mdel for MainWindow
    /// </summary>
    public class MainWindowModel : INotifyPropertyChanged
    {
        #region form fields backend
        private string userName;
        private string location;
        private string language;
        private int? numberOfRepos;
        private string contributedIn;
        private string errorMessage;

        private ObservableCollection<User> searchResults;
        private Dictionary<int, User> fullyLoadedResults;
        #endregion

        #region public model properties
        public string UserName
        {
            get
            {
                return userName;
            }
            set
            {
                userName = value;
                OnPropertyChanged("UserName");
            }
        }
        public string Location
        {
            get
            {
                return location;
            }
            set
            {
                location = value;
                OnPropertyChanged("Location");
            }
        }
        public string Language
        {
            get
            {
                return language;
            }
            set
            {
                language = value;
                OnPropertyChanged("Language");
            }
        }
        public int? NumberOfRepos
        {
            get
            {
                return numberOfRepos;
            }
            set
            {
                numberOfRepos = value;
                OnPropertyChanged("NumberOfRepos");
            }
        }
        public string ContributedIn
        {
            get
            {
                return contributedIn;
            }
            set
            {
                contributedIn = value;
                OnPropertyChanged("ContributedIn");
            }
        }
        public string StatusMessage
        {
            get
            {
                return errorMessage;
            }
            set
            {
                errorMessage = value;
                OnPropertyChanged("StatusMessage");
            }
        }

        public ObservableCollection<User> SearchResults
        {
            get
            {
                return searchResults;
            }
            set
            {
                searchResults = value;
                OnPropertyChanged("SearchResults");
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// public ctor
        /// </summary>
        public MainWindowModel()
        {
            UserName = "alekol";
            Location = "bulgaria";
            Language = "csharp";
            
            SearchResults = new ObservableCollection<User>();
            fullyLoadedResults = new Dictionary<int, User>();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Returns one or more query strings that intersected represent the expected result of the search criteria
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetQueryString()
        {
            string mainUserQuery = string.Empty;

            mainUserQuery += GetQueryCriteria("location", Location);
            mainUserQuery += GetQueryCriteria("language", Language);
            mainUserQuery += GetQueryCriteria("repos", NumberOfRepos != null ? NumberOfRepos.ToString() : string.Empty);

            yield return mainUserQuery;
        }

        /// <summary>
        /// Returns a list of users whose data is not yet cached in the model
        /// </summary>
        /// <param name="startIdx">Start index of the first user</param>
        /// <param name="count">Number of users to scan</param>
        /// <returns>
        /// A list of tuples - user index in the collection and partial user data to load
        /// This doesn't return a yielded iterator, as that requires synchronization in async context
        /// </returns>
        public IEnumerable<Tuple<int, User>> GetUsersToLoad(int startIdx, int count)
        {
            if (startIdx >= searchResults.Count)
                throw new ArgumentOutOfRangeException("startIdx");

            if (startIdx + count > searchResults.Count )
                throw new ArgumentOutOfRangeException("count");

            List<Tuple<int, User>> res = new List<Tuple<int, User>>();

            for (int i=startIdx; i < startIdx + count; ++i )
            {
                var user = searchResults[i];
                if (!fullyLoadedResults.ContainsKey(user.Id))
                    res.Add(new Tuple<int, User>(i, user));
                else
                    searchResults[i] = fullyLoadedResults[user.Id];
            }

            return res;
        }

        /// <summary>
        /// Returns a serializable collection of user data
        /// </summary>
        public IEnumerable<SerializableUser> SerializableResults
        {
            get
            {
                foreach(var usr in searchResults)
                {
                    yield return new SerializableUser(usr);
                }
            }
        }

        /// <summary>
        /// Caches a fully-loaded user record
        /// </summary>        
        /// <param name="userIdx">Idx in the search results</param>
        /// <param name="usr">User data</param>
        public void AddUserToCache(int userIdx, User usr)
        {
            if (userIdx < 0 || userIdx > searchResults.Count)
                return;

            searchResults[userIdx] = usr;
            fullyLoadedResults[usr.Id] = usr;
            OnPropertyChanged("SearchResults");
        }

        private string GetQueryCriteria(string apiParamName, string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyValue))
                return string.Empty;

            return string.Format(" {0}:{1}", apiParamName, propertyValue);
        }
    }
}
