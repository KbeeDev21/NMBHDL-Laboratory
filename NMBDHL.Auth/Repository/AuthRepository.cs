using NMBDHL.DataAccess.SQLDataBaseAccess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NMBDHL.Auth.Repository
{
    public class AuthRepository
    {
        private readonly ISQLDataBaseAccess _db;
        public AuthRepository(ISQLDataBaseAccess db)
        {
            _db = db;
        }
    }
}
