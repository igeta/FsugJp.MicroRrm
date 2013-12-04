namespace FsugJp.MicroRrm

open System
open System.Data
open System.Reflection
open Microsoft.FSharp.Reflection

[<AutoOpen>]
module DbExtensions =
    type System.Data.Common.DbProviderFactory with
        member f.CreateConnection(connectionString) =
            let cnn = f.CreateConnection()
            cnn.ConnectionString <- connectionString
            cnn

        member f.CreateOpenConnection(connectionString) =
            let cnn = f.CreateConnection(connectionString)
            cnn.Open()
            cnn

    type System.Data.IDbConnection with
        member cnn.CreateCommand(commandText) =
            let cmd = cnn.CreateCommand()
            cmd.CommandText <- commandText
            cmd

    type System.Data.IDataReader with
        member r.GetFineValue(i) =
            let value = r.GetValue(i)
            if DBNull.Value.Equals(value) then null else value

[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)>]
type DbSchemaAttribute() =
    inherit Attribute()

type DbRecordFieldInfo =
    { Index : int; Name : string; Type : Type; Value : obj }

[<AutoOpen>]
module Internals =
    let inline assertBy f x = assert (f x); x

    let dynamicDefaultOf (typ: Type) = if typ.IsValueType then Activator.CreateInstance(typ) else null

    let dbRecordBindflgs = BindingFlags.GetProperty ||| BindingFlags.Instance ||| BindingFlags.Public

    let inline isDbRecord typ = FSharpType.IsRecord(typ, dbRecordBindflgs)

    let inline getDbRecordFields typ = FSharpType.GetRecordFields(typ, dbRecordBindflgs)

    let makeDbRecord<'R when 'R : not struct> =
        let rty = assertBy isDbRecord typeof<'R>
        let mk = FSharpValue.PreComputeRecordConstructor(rty, dbRecordBindflgs)
        (fun vals -> mk vals :?> 'R)

    let isDbSchema (typ: Type) =
        FSharpType.IsModule typ
        && Attribute.GetCustomAttributes(typ, typeof<DbSchemaAttribute>).Length > 0

    let dbRecordName<'R when 'R : not struct> =
        let rty = assertBy isDbRecord typeof<'R>
        let dty = rty.DeclaringType
        let mname = if dty <> null && isDbSchema dty then dty.Name + "." else ""
        mname + rty.Name
        
    let dbRecordFields<'R when 'R : not struct> =
        let rty = assertBy isDbRecord typeof<'R>
        getDbRecordFields rty
        |> Seq.mapi (fun i p ->
             let typ = p.PropertyType
             { Index=i; Name=p.Name; Type=typ; Value=dynamicDefaultOf typ })

    let dynamicMakeOptionType (typ: Type) =
        typedefof<Option<_>>.MakeGenericType([|typ|])

    let dynamicMakeOptionValue (typ: Type) (value: obj) =
        let tag, varr = if value = null then 0, [||] else 1, [|value|]
        let case = FSharpType.GetUnionCases(dynamicMakeOptionType(typ)) |> Seq.find (fun uc -> uc.Tag = tag)
        FSharpValue.MakeUnion(case, varr)

    let optionTypeParameter (typ : Type) =
        let isOpt = typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<Option<_>>
        if isOpt then Some (typ.GetGenericArguments().[0]) else None

    let dbTableFields (schema: DataTable) = 
        seq { for drow in schema.Rows do
              let anull : bool = downcast drow.["AllowDBNull"]
              let typ   : Type = downcast drow.["DataType"]
              let typ = if anull then dynamicMakeOptionType typ else typ
              yield { Index = downcast drow.["ColumnOrdinal"];
                      Name  = downcast drow.["ColumnName"];
                      Type  = typ;
                      Value = dynamicDefaultOf typ;
                    }
        }

    let fieldMapper<'R when 'R : not struct> (schema: DataTable) =
        let schema =
            dbTableFields schema
            |> Seq.map (fun fld -> fld.Name, fld)
            |> dict
        dbRecordFields<'R>
        |> Seq.map (fun fld ->
            let idx = if schema.ContainsKey(fld.Name) then schema.[fld.Name].Index else -1
            { fld with Index = idx })
        |> Seq.toArray

module DbRecord =
    let readBy<'R when 'R : not struct> (cnn: IDbConnection) sql =
        seq { use cmd    = cnn.CreateCommand(sql)
              use reader = cmd.ExecuteReader()
              let fieldMapInfo = using (reader.GetSchemaTable()) fieldMapper<'R>
              while reader.Read() do
              yield fieldMapInfo
                    |> Array.map (fun fld ->
                         let value = if fld.Index < 0 then null else reader.GetFineValue(fld.Index)
                         match optionTypeParameter fld.Type with
                         | Some typ -> dynamicMakeOptionValue typ value
                         | None     -> value)
                    |> makeDbRecord<'R>
        }

    let read<'R when 'R : not struct> (cnn: IDbConnection) =
        readBy<'R> cnn ("SELECT * FROM " + dbRecordName<'R>)
