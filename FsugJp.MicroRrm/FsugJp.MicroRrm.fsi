namespace FsugJp.MicroRrm

open System
open System.Data
open System.Data.Common

[<AutoOpen>]
module DbExtensions = begin
    type System.Data.Common.DbProviderFactory with
        member CreateConnection     : connectionString:string -> DbConnection
        member CreateOpenConnection : connectionString:string -> DbConnection
end

[<AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)>]
type DbSchemaAttribute = class
    inherit System.Attribute
    new : unit -> DbSchemaAttribute
end

module DbRecord = begin
    val readBy  : cnn:IDbConnection -> sql:string -> seq<'R> when 'R : not struct
    val read    : cnn:IDbConnection -> seq<'R> when 'R : not struct
end
