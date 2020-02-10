using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Logic.Model
{
    public class DatabaseConfigurationViewModel : IDataErrorInfo
    {
        public DatabaseConfigurationViewModel(String serverAddress, String databaseName, String userName, String password, String tableName)
        {
            ServerAddress = serverAddress;
            DatabaseName = databaseName;
            UserName = userName;
            Password = password;
            TableName = tableName;
        }

        public String ServerAddress { get; set; }

        public String DatabaseName { get; set; }

        public String UserName { get; set; }

        public String Password { get; set; }

        public String TableName { get; set; }


        public Int32 ErrorsCount { get; set; }

        public Boolean IsValid
        {
            get
            {
                return ErrorsCount <= 0 &&
                       !String.IsNullOrEmpty(Password);
            }
        }

        #region IDataErrorInfo Members

        string IDataErrorInfo.Error
        {
            get { return null; }
        }

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case "ServerAddress":
                        if (String.IsNullOrEmpty(ServerAddress)) return "Server address is required";
                        break;
                    case "UserName":
                        if (String.IsNullOrEmpty(UserName)) return "User name is required";
                        break;
                    case "DatabaseName":
                        if (String.IsNullOrEmpty(DatabaseName)) return "Database name is required";
                        break;
                    case "TableName":
                        if (String.IsNullOrEmpty(TableName)) return "Table name is required";
                        break;
                }

                return null;
            }
        }
        #endregion
    }
}
