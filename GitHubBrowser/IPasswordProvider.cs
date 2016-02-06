using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitUserBrowser
{
    public interface IPasswordProvider
    {
        string GetPassword();
    }
}
