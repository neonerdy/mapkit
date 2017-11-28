# mapkit

Lightweight data mapper framework

## How to Use


```
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapKit;

namespace ConsoleApplication3
{
    [Table(Name="FolderAccess")]
    public class FolderAccess : DbHelper 
    {
        [Id(Name = "SetId")]
        public Guid SetId { get; set; }

        [Column(Name = "GroupName")]
        public string GroupName { get; set; }

        [Column(Name = "AccessTypeId")]
        public Guid AccessTypeId { get; set; }

        [Column(Name = "AccessTypeName",IsEntityRef=true)]
        public string AccessTypeName { get; set; }

        public SetAccess(DataSource ds)
            : base(ds)
        {

        }

        public FolderAccess()
        {

        }


        public List<FolderAccess> GetAll()
        {
            string sql = @"SELECT fa.FolderId,fa.GroupName,at.ID as AccessTypeId,at.AccessTypeName from FolderAccess fa
                          INNER JOIN AccessTypes at on at.ID=fa.AccessTypeId";

            return GetByQuery<FolderAccess>(sql);
        }


    }
}


```



```
 static void Main(string[] args)
 {
          var ds = new DataSource("System.Data.SqlClient","Data Source=localhost;Initial Catalog=Db1;User Id=sa;Password=Password1");
          var fa = new FolderAccess(ds);
          var list=fa.GetAll();

          foreach (var f in list)
          {
             Console.WriteLine(f.FolderId + "-" + s.GroupName + "-" + s.AccessTypeName);
          }
 }

```


