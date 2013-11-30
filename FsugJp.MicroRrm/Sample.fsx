#r @".\bin\Debug\FsugJp.MicroRrm.dll"

open System
open System.Data.Common
open FsugJp.MicroRrm

[<DbSchema>]
module Sales =
    type Customer =
        { CustomerID    : int;
          //PersonID      : int option;
          //StoreID       : int option;
          TerritoryID   : int option;
          AccountNumber : string;
          //rowguid       : Guid;
          ModifiedDate  : DateTime;
        }

module Program =
    let envvar var = Environment.ExpandEnvironmentVariables(var)

    // Adventure Works for SQL Server 2012: http://msftdbprodsamples.codeplex.com/releases/view/55330
    let provider = @"System.Data.SqlClient"
    let cnnstr   = @"Data Source=(LocalDB)\v11.0;Integrated Security=True;"
                 + @"AttachDbFilename=" + envvar "%userprofile%\LocalDB\AdventureWorks2012_Data.mdf"

    let main _ =
        let factory = DbProviderFactories.GetFactory(provider)
        use cnn = factory.CreateOpenConnection(cnnstr)

        DbRecord.read<Sales.Customer> cnn
        |> Seq.truncate 10
        |> Seq.iter (printfn "%A")

        printfn "%s" (String.init 64 (fun _ -> "-"))

        "SELECT * FROM Sales.Customer WHERE TerritoryID = 1"
        |> DbRecord.readBy<Sales.Customer> cnn
        |> Seq.truncate 10
        |> Seq.iter (printfn "%A")

open Program

main ()
