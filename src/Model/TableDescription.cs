using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Model;

public class TableDescription
{
    const int DefaultReadCapacityUnits = 5;
    const int DefaultWriteCapacityUnits = 2;

    TableDescription(string tableName, ITypeContract contract)
    {
        TableName = tableName;
        PartitionKeyProperty = ResolveIndexKeyProperty<PartitionKeyAttribute>(contract, required: true);
        SortKeyProperty = ResolveIndexKeyProperty<SortKeyAttribute>(contract);
        VersionProperty = ResolveVersionProperty(contract);
        LocalSecondaryIndexSortKeyProperties = ResolveSecondaryIndexKeyProperties<SortKeyAttribute>(contract, IndexType.LocalSecondaryIndex);
        GlobalSecondaryIndexPartitionKeyProperties = ResolveSecondaryIndexKeyProperties<PartitionKeyAttribute>(contract, IndexType.GlobalSecondaryIndex);
        GlobalSecondaryIndexSortKeyProperties = ResolveSecondaryIndexKeyProperties<SortKeyAttribute>(contract, IndexType.GlobalSecondaryIndex);

        FallBackToPrimaryPartitionKey(
            GlobalSecondaryIndexPartitionKeyProperties,
            GlobalSecondaryIndexSortKeyProperties,
            PartitionKeyProperty);
    }

    public string TableName { get; }

    public ITypeContractProperty PartitionKeyProperty { get; }

    public ITypeContractProperty SortKeyProperty { get; }

    public ITypeContractProperty VersionProperty { get; }
 
    public ITypeContractProperty[] LocalSecondaryIndexSortKeyProperties { get; }
 
    public ITypeContractProperty[] GlobalSecondaryIndexPartitionKeyProperties { get; }
 
    public ITypeContractProperty[] GlobalSecondaryIndexSortKeyProperties { get; }


    public static TableDescription Get(Type type, ITypeContractResolver contractResolver)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(contractResolver);

        var tableName = GetTableName(type);
        var contract = contractResolver.ResolveContract(type);

        return new TableDescription(tableName, contract);
    }

    public static string GetTableName<T>(DynamoDBClientOptions options = null) => GetTableName(typeof(T), options);

    public static string GetTableName(Type type, DynamoDBClientOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        var tableAttribute = type.GetTypeInfo().GetCustomAttribute<TableAttribute>(inherit: true);
        if (tableAttribute == null)
            throw new InvalidOperationException($"Type {type.Name} is missing a Table attribute");

        var name = tableAttribute.Name ?? type.Name.ToHyphenCasing().NaivelyPluralized();
        
        return ApplyTableNamePrefixAndMapping(options, name);
    }

    public string GetIndexName(MemberInfo partitionKey, MemberInfo sortKey)
    {
        ArgumentNullException.ThrowIfNull(partitionKey);

        var partitionKeyAttributes =
            partitionKey.GetCustomAttributes<PartitionKeyAttribute>().
                OrderBy(attribute => attribute.Type).
                ThenBy(attribute => attribute.Ordinal).
                ToArray();

        if (partitionKeyAttributes.Length == 0)
            throw new ArgumentOutOfRangeException(
                nameof(partitionKey),
                "Not a valid primary key, local secondary index or global secondary index");

        if (sortKey == null)
        {
            switch (partitionKeyAttributes[0].Type)
            {
                case IndexType.PrimaryKey:
                    return null;

                case IndexType.GlobalSecondaryIndex:
                    return GetGlobalSecondaryIndexName(partitionKeyAttributes[0].Ordinal);
            }
        }

        var sortKeyAttributes =
            sortKey.GetCustomAttributes<SortKeyAttribute>().
                OrderBy(attribute => attribute.Type).
                ThenBy(attribute => attribute.Ordinal).
                ToArray();

        if (sortKeyAttributes.Length == 0)
            throw new ArgumentOutOfRangeException(
                nameof(sortKey),
                "Not a valid primary key, local secondary index or global secondary index");

        for (var i = 0; i < partitionKeyAttributes.Length; i++)
        {
            var j =
                Array.FindIndex(
                    sortKeyAttributes,
                    attribute =>
                        (partitionKeyAttributes[i].Type == IndexType.PrimaryKey
                            ? (attribute.Type == IndexType.PrimaryKey || attribute.Type == IndexType.LocalSecondaryIndex)
                            : (attribute.Type == IndexType.GlobalSecondaryIndex && attribute.Ordinal == partitionKeyAttributes[i].Ordinal)));

            if (j >= 0)
            {
                switch (sortKeyAttributes[j].Type)
                {
                    case IndexType.PrimaryKey:
                        return null;

                    case IndexType.LocalSecondaryIndex:
                        return GetLocalSecondaryIndexName(sortKeyAttributes[j].Ordinal);

                    case IndexType.GlobalSecondaryIndex:
                        return GetGlobalSecondaryIndexName(sortKeyAttributes[j].Ordinal);
                }
            }
        }

        throw new ArgumentOutOfRangeException(nameof(sortKey), "Not part of the same key/index as parameter partitionKey.");
    }

    public CreateTableRequest GetCreateTableRequest(
        DynamoDBClientOptions options = null,
        ProvisionedThroughput provisionedThroughput = null,
        Projection projection = null,
        StreamSpecification streamSpecification = null,
        Func<Type, ScalarAttributeType> mapToKeyAttributeType = null) =>
        TableRequests.CreateTable(this, options, provisionedThroughput, projection, streamSpecification, mapToKeyAttributeType);

    public UpdateTableRequest GetUpdateTableProvisionedThroughputRequest(
        DynamoDBClientOptions options = null, 
        int? readCapacityUnits = null, 
        int? writeCapacityUnits = null) =>
        TableRequests.UpdateTableProvisionedThroughput(this, options, readCapacityUnits, writeCapacityUnits);


    public static class KeyTypes<T>
    {
        static KeyTypes()
        {
            var tableDescription = Get(typeof(T), TypeContractResolver.Default);

            PartitionKey = tableDescription.PartitionKeyProperty.PropertyType;
            SortKey = tableDescription.SortKeyProperty?.PropertyType;
        }

        public static readonly Type PartitionKey;
        public static readonly Type SortKey;
    }     

    public static class PropertyNames<T>
    {
        static PropertyNames()
        {
            var tableDescription = Get(typeof(T), TypeContractResolver.Default);

            PartitionKey = tableDescription.PartitionKeyProperty.PropertyName;
            SortKey = tableDescription.SortKeyProperty?.PropertyName;
            Version = tableDescription.VersionProperty?.PropertyName;
        }

        public static readonly string PartitionKey;
        public static readonly string SortKey;
        public static readonly string Version;
    }  

    public static class PropertyAccessors<T>
    {
        static PropertyAccessors()
        {
            var tableDescription = Get(typeof(T), TypeContractResolver.Default);

            GetPartitionKey = tableDescription.PartitionKeyProperty.CompileGetter<T>();
            GetSortKey = tableDescription.SortKeyProperty?.CompileGetter<T>();
            GetVersion = tableDescription.VersionProperty?.CompileGetter<T>();
        }

        public static readonly Func<T, object> GetPartitionKey;
        public static readonly Func<T, object> GetSortKey;
        public static readonly Func<T, object> GetVersion;
    }     
    

    string GetLocalSecondaryIndexName(int ordinal) =>
        GetPropertyIndexAttributeName<SortKeyAttribute>(LocalSecondaryIndexSortKeyProperties[ordinal], IndexType.LocalSecondaryIndex, ordinal) ??
        $"lsi-{ordinal}-{LocalSecondaryIndexSortKeyProperties[ordinal].PropertyName.ToHyphenCasing()}";

    string GetGlobalSecondaryIndexName(int ordinal)
    {
        var name = GetPropertyIndexAttributeName<PartitionKeyAttribute>(GlobalSecondaryIndexPartitionKeyProperties[ordinal], IndexType.GlobalSecondaryIndex, ordinal);
        if (name != null)
            return name;

        name = $"gsi-{ordinal}-{GlobalSecondaryIndexPartitionKeyProperties[ordinal].PropertyName.ToHyphenCasing()}";

        if (GlobalSecondaryIndexSortKeyProperties[ordinal] != null)
            name += $"-{GlobalSecondaryIndexSortKeyProperties[ordinal].PropertyName.ToHyphenCasing()}";

        return name;
    }

    static IEnumerable<int> GetIndexOrdinals(IndexType type)
    {
        switch (type) 
        {
            case IndexType.LocalSecondaryIndex: 
                return Enumerable.Range(0, Base.IndexKeyAttributeBase.MaxNumberOfLocalSecondaryIndexes);
            
            case IndexType.GlobalSecondaryIndex: 
                return Enumerable.Range(0, Base.IndexKeyAttributeBase.MaxNumberOfGlobalSecondaryIndexes);

            default:
                return Enumerable.Empty<int>();
        }
    }

    static string GetPropertyIndexAttributeName<TAttribute>(ITypeContractProperty property, IndexType type, int ordinal) where TAttribute : Base.IndexKeyAttributeBase =>
        property.GetAttributes<TAttribute>().FirstOrDefault(a => a.Type == type && a.Ordinal == ordinal)?.Name;

    static ITypeContractProperty ResolveIndexKeyProperty<TAttribute>(ITypeContract contract, IndexType type = IndexType.PrimaryKey, int ordinal = 0, bool required = false) where TAttribute : Base.IndexKeyAttributeBase
    {
        var properties =
            contract.Properties.
                Where(p => p.GetAttributes<TAttribute>().Any(a => a.Type == type && a.Ordinal == ordinal)).
                ToArray();

        var attributeDescription = typeof(TAttribute).Name;
        if (type != IndexType.PrimaryKey)
            attributeDescription += $"({type} = {ordinal})";

        return ValidSingleResolvedPropertyResult(contract, properties, attributeDescription, required);
    }

    static ITypeContractProperty ResolveVersionProperty(ITypeContract contract) =>
        ValidSingleResolvedPropertyResult(
            contract, 
            contract.Properties.Where(p => p.HasAttribute<VersionAttribute>()).ToArray(), 
            typeof(Version).Name);

    static ITypeContractProperty[] ResolveSecondaryIndexKeyProperties<TAttribute>(ITypeContract contract, IndexType type) where TAttribute : Base.IndexKeyAttributeBase =>
        GetIndexOrdinals(type).Select(ordinal => ResolveIndexKeyProperty<TAttribute>(contract, type, ordinal)).ToArray();

    static ITypeContractProperty ValidSingleResolvedPropertyResult(ITypeContract contract, ITypeContractProperty[] properties, string attributeDescription, bool required = false)
    {
        if (properties.Length > 1)
            throw new InvalidOperationException($"Expected at most one property with a {attributeDescription} attribute for {contract.UnderlyingType.FullName}, got {properties.Length}");

        if (properties.Length == 0)
        {
            if (required)
                throw new InvalidOperationException($"Expected a property with a {attributeDescription} attribute for {contract.UnderlyingType.FullName}");

            return null;
        }

        return properties[0];
    }

    static void FallBackToPrimaryPartitionKey(ITypeContractProperty[] indexPartitionKeyProperties, ITypeContractProperty[] indexSortKeyProperties, ITypeContractProperty primaryPartitionKey)
    {
        for (var i = 0; i < indexPartitionKeyProperties.Length; i++)
        {
            if (indexPartitionKeyProperties[i] == null && indexSortKeyProperties[i] != null)
                indexPartitionKeyProperties[i] = primaryPartitionKey;
        }
    }

    static string ApplyTableNamePrefixAndMapping(DynamoDBClientOptions options, string name)
    {
        string mappedName;
        return
            options == null 
            ? name
            : options.TableNameMappings.TryGetValue(name, out mappedName)
                ? mappedName
                : options.TableNamePrefix + name;
    }

    static class TableRequests
    {
        public static CreateTableRequest CreateTable(
            TableDescription table,
            DynamoDBClientOptions options = null,
            ProvisionedThroughput provisionedThroughput = null, 
            Projection projection = null, 
            StreamSpecification streamSpecification = null,
            Func<Type, ScalarAttributeType> mapToKeyAttributeType = null) =>
            new CreateTableRequest
            {
                TableName = ApplyTableNamePrefixAndMapping(options, table.TableName),
                KeySchema = GetKeySchema(table.PartitionKeyProperty, table.SortKeyProperty),
                ProvisionedThroughput = provisionedThroughput ?? GetDefaultProvisionedThrougput(),
                StreamSpecification = streamSpecification ?? GetDefaultStreamSpecification(),
                SSESpecification = new SSESpecification { Enabled = true },
                AttributeDefinitions = (
                    from property in 
                        new[] { table.PartitionKeyProperty, table.SortKeyProperty }.
                        Concat(table.LocalSecondaryIndexSortKeyProperties).
                        Concat(table.GlobalSecondaryIndexPartitionKeyProperties).
                        Concat(table.GlobalSecondaryIndexSortKeyProperties)
                    where property != null
                    group property by property.PropertyName into propertiesPerName
                    let property = propertiesPerName.First()
                    select new AttributeDefinition
                    {
                        AttributeName = property.PropertyName,
                        AttributeType = 
                            mapToKeyAttributeType?.Invoke(property.PropertyType) ?? 
                            GetScalarAttributeType(property.PropertyType)
                    }).
                    ToList(),

                LocalSecondaryIndexes = (
                    from ordinal in GetIndexOrdinals(IndexType.LocalSecondaryIndex)
                    let sortKey = table.LocalSecondaryIndexSortKeyProperties[ordinal]
                    where sortKey != null
                    select new LocalSecondaryIndex
                    {
                        IndexName = table.GetLocalSecondaryIndexName(ordinal),
                        KeySchema = GetKeySchema(table.PartitionKeyProperty, sortKey),
                        Projection = projection ?? GetDefaultProjection()
                    }).
                    ToList(),

                GlobalSecondaryIndexes = (
                    from ordinal in GetIndexOrdinals(IndexType.GlobalSecondaryIndex)
                    let partitionKey = table.GlobalSecondaryIndexPartitionKeyProperties[ordinal]
                    let sortKey = table.GlobalSecondaryIndexSortKeyProperties[ordinal]
                    where partitionKey != null
                    select new GlobalSecondaryIndex
                    {
                        IndexName = table.GetGlobalSecondaryIndexName(ordinal),
                        KeySchema = GetKeySchema(partitionKey, sortKey),
                        Projection = projection ?? GetDefaultProjection(),
                        ProvisionedThroughput = provisionedThroughput ?? GetDefaultProvisionedThrougput()
                    }).
                    ToList(),
            };

        public static UpdateTableRequest UpdateTableProvisionedThroughput(
            TableDescription table,
            DynamoDBClientOptions options = null,
            int? readCapacityUnits = null,
            int? writeCapacityUnits = null) =>
            new UpdateTableRequest
            {
                TableName = ApplyTableNamePrefixAndMapping(options, table.TableName),
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = readCapacityUnits ?? DefaultReadCapacityUnits,
                    WriteCapacityUnits = writeCapacityUnits ?? DefaultWriteCapacityUnits,
                },
                GlobalSecondaryIndexUpdates = (
                    from ordinal in GetIndexOrdinals(IndexType.GlobalSecondaryIndex)
                    let partitionKey = table.GlobalSecondaryIndexPartitionKeyProperties[ordinal]
                    let sortKey = table.GlobalSecondaryIndexSortKeyProperties[ordinal]
                    where partitionKey != null
                    select new GlobalSecondaryIndexUpdate
                    {
                        Update = new UpdateGlobalSecondaryIndexAction
                        {
                            IndexName = table.GetGlobalSecondaryIndexName(ordinal),
                            ProvisionedThroughput = new ProvisionedThroughput
                            {
                                ReadCapacityUnits = readCapacityUnits ?? DefaultReadCapacityUnits,
                                WriteCapacityUnits = writeCapacityUnits ?? DefaultWriteCapacityUnits,
                            }
                        }
                    }).
                    ToList(),
            };


        static List<KeySchemaElement> GetKeySchema(ITypeContractProperty partitionKeyProperty, ITypeContractProperty sortKeyProperty)
        {
            var elements =
                new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = partitionKeyProperty.PropertyName,
                        KeyType = KeyType.HASH
                    }
                };

            if (sortKeyProperty != null)
            {
                elements.Add(
                    new KeySchemaElement
                    {
                        AttributeName = sortKeyProperty.PropertyName,
                        KeyType = KeyType.RANGE
                    });
            }

            return elements;
        }

        static ProvisionedThroughput GetDefaultProvisionedThrougput() =>
            new ProvisionedThroughput
            {
                ReadCapacityUnits = DefaultReadCapacityUnits,
                WriteCapacityUnits = DefaultWriteCapacityUnits
            };

        static Projection GetDefaultProjection() =>
            new Projection
            {
                ProjectionType = ProjectionType.ALL
            };

        static StreamSpecification GetDefaultStreamSpecification() =>
            new StreamSpecification
            {
                StreamEnabled = true,
                StreamViewType = StreamViewType.NEW_AND_OLD_IMAGES
            };

        static ScalarAttributeType GetScalarAttributeType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return ScalarAttributeType.N;

                default:
                    return
                        type == typeof(byte[]) 
                        ? ScalarAttributeType.B 
                        : ScalarAttributeType.S;
            }
        }
    }
}
