using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graduation.BLL.Services.Interfaces
{
    public interface ILanguageService
    {
        string CurrentLanguage { get; }   
        string GetMessage(string key, params object[] args);
    }

}
