using NMBDHL.Auth.Model;
using NMBDHL.DataAccess.SQLDataBaseAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMBDHL.Auth.Repository
{
    public interface IAuthRepository
    {
        Task<User> ValidateUser(Login login);
    }
}
