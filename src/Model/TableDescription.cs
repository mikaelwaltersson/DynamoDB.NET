using System.Collections.Concurrent;
using System.Reflection;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDB.Net.Serialization;

namespace DynamoDB.Net.Model;

public class TableDescription
{
    const int DefaultReadCapacityUnits = 5;
    
    const int DefaultWriteCapacityUnits = 2;


    TableDescription(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        TableName = GetTableName(type);
        PartitionKeyProperty = GetIndexKeyProperty<PartitionKeyAttribute>(type, required: true)!;
        SortKeyProperty = GetIndexKeyProperty<SortKeyAttribute>(type);
        VersionProperty = GetVersionProperty(type);
        LocalSecondaryIndexSortKeyProperties = GetSecondaryIndexKeyProperties<SortKeyAttribute>(type, IndexType.LocalSecondaryIndex);
        GlobalSecondaryIndexSortKeyProperties = GetSecondaryIndexKeyProperties<SortKeyAttribute>(type, IndexType.GlobalSecondaryIndex);
        GlobalSecondaryIndexPartitionKeyProperties = GetSecondaryIndexKeyProperties<PartitionKeyAttribute>(type, IndexType.GlobalSecondaryIndex, GlobalSecondaryIndexSortKeyProperties);
    }

    public string TableName { get; }

    public MemberInfo PartitionKeyProperty { get; }

    public MemberInfo? SortKeyProperty { get; }

    public MemberInfo? VersionProperty { get; }
 
    public MemberInfo?[] LocalSecondaryIndexSortKeyProperties { get; }
 
    public MemberInfo?[] GlobalSecondaryIndexPartitionKeyProperties { get; }
 
    public MemberInfo?[] GlobalSecondaryIndexSortKeyProperties { get; }
    
    static readonly ConcurrentDictionary<Type, TableDescription> cachedTableDescriptions = [];

    public static TableDescription Get(Type type) =>
        cachedTableDescriptions.GetOrAdd(type, static type => new(type));

    public static string GetTableName<T>(DynamoDBClientOptions? options = null) => GetTableName(typeof(T), options);

    public static string GetTableName(Type type, DynamoDBClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(type);

        var tableAttribute = 
            type.GetCustomAttribute<TableAttribute>(inherit: true) ?? 
            throw new InvalidOperationException($"Type {type.Name} is missing a Table attribute");

        var tableName = tableAttribute.TableName ?? type.Name.ToHyphenCase().NaivelyPluralized();
        
        return ApplyTableNamePrefixAndMapping(options, tableName);
    }

    public string? GetIndexName(MemberInfo partitionKey, MemberInfo? sortKey)
    {
        ArgumentNullException.ThrowIfNull(partitionKey);

        var partitionKeyAttributes =
            partitionKey
                .GetCustomAttributes<PartitionKeyAttribute>()
                .OrderBy(attribute => attribute.IndexType)
                .ThenBy(attribute => attribute.Ordinal)
                .ToArray();

        if (partitionKeyAttributes.Length == 0)
            throw new ArgumentOutOfRangeException(
                nameof(partitionKey),
                "Not a valid primary key, local secondary index or global secondary index");

        if (sortKey == null)
        {
            return 
                partitionKeyAttributes[0].IndexType switch
                {
                    IndexType.PrimaryKey => 
                        null,
                    
                    IndexType.GlobalSecondaryIndex => 
                        GetGlobalSecondaryIndexName(partitionKeyAttributes[0].Ordinal),

                    _ => 
                        throw new ArgumentNullException(nameof(sortKey))
                };
        }

        var sortKeyAttributes =
            sortKey.GetCustomAttributes<SortKeyAttribute>()
                .OrderBy(attribute => attribute.IndexType)
                .ThenBy(attribute => attribute.Ordinal)
                .ToArray();

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
                        (partitionKeyAttributes[i].IndexType == IndexType.PrimaryKey
                            ? (attribute.IndexType == IndexType.PrimaryKey || attribute.IndexType == IndexType.LocalSecondaryIndex)
                            : (attribute.IndexType == IndexType.GlobalSecondaryIndex && attribute.Ordinal == partitionKeyAttributes[i].Ordinal)));

            if (j >= 0)
            {
                switch (sortKeyAttributes[j].IndexType)
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
        IDynamoDBSerializer serializer,
        DynamoDBClientOptions? options = null,
        ProvisionedThroughput? provisionedThroughput = null,
        Projection? projection = null,
        StreamSpecification? streamSpecification = null,
        Func<Type, ScalarAttributeType>? mapToKeyAttributeType = null) =>
        TableRequests.CreateTable(this, serializer, options, provisionedThroughput, projection, streamSpecification, mapToKeyAttributeType);

    public UpdateTableRequest GetUpdateTableProvisionedThroughputRequest(
        DynamoDBClientOptions? options = null, 
        int? readCapacityUnits = null, 
        int? writeCapacityUnits = null) =>
        TableRequests.UpdateTableProvisionedThroughput(this, options, readCapacityUnits, writeCapacityUnits);    

    string GetLocalSecondaryIndexName(int ordinal) =>
        GetPropertyIndexAttributeName<SortKeyAttribute>(LocalSecondaryIndexSortKeyProperties[ordinal], IndexType.LocalSecondaryIndex, ordinal) ??
        $"lsi-{ordinal}-{LocalSecondaryIndexSortKeyProperties[ordinal]?.Name.ToHyphenCase()}";

    string GetGlobalSecondaryIndexName(int ordinal)
    {
        var name = GetPropertyIndexAttributeName<PartitionKeyAttribute>(GlobalSecondaryIndexPartitionKeyProperties[ordinal], IndexType.GlobalSecondaryIndex, ordinal);
        if (name != null)
            return name;

        name = $"gsi-{ordinal}-{GlobalSecondaryIndexPartitionKeyProperties[ordinal]?.Name.ToHyphenCase()}";

        if (GlobalSecondaryIndexSortKeyProperties[ordinal] != null)
            name += $"-{GlobalSecondaryIndexSortKeyProperties[ordinal]?.Name.ToHyphenCase()}";

        return name;
    }

    static IEnumerable<int> GetIndexOrdinals(IndexType type) => 
        type switch
        {
            IndexType.LocalSecondaryIndex => 
                Enumerable.Range(0, IndexKeyAttribute.MaxNumberOfLocalSecondaryIndexes),
            
            IndexType.GlobalSecondaryIndex => 
                Enumerable.Range(0, IndexKeyAttribute.MaxNumberOfGlobalSecondaryIndexes),
            
            _ => [],
        };

    static string? GetPropertyIndexAttributeName<TAttribute>(MemberInfo? property, IndexType indexType, int ordinal) where TAttribute : IndexKeyAttribute =>
        property?.GetCustomAttributes<TAttribute>().FirstOrDefault(a => a.IndexType == indexType && a.Ordinal == ordinal)?.IndexName;

    static MemberInfo? GetIndexKeyProperty<TAttribute>(Type type, IndexType indexType = IndexType.PrimaryKey, int ordinal = 0, bool required = false) where TAttribute : IndexKeyAttribute
    {
        var properties =
            type.GetSerializablePropertiesAndFields()
                .Where(p => p.GetCustomAttributes<TAttribute>().Any(a => a.IndexType == indexType && a.Ordinal == ordinal))
                .ToArray();

        var attributeDescription = typeof(TAttribute).Name;
        if (indexType != IndexType.PrimaryKey)
            attributeDescription += $"({indexType} = {ordinal})";

        return ValidSingleResolvedPropertyResult(type, properties, attributeDescription, required);
    }

    static MemberInfo? GetVersionProperty(Type type) =>
        ValidSingleResolvedPropertyResult(
            type,
            type.GetSerializablePropertiesAndFields().Where(property => property.HasCustomAttribute<VersionAttribute>()).ToArray(), 
            typeof(Version).Name);

    static MemberInfo?[] GetSecondaryIndexKeyProperties<TAttribute>(Type type, IndexType indexType, MemberInfo?[]? relatedIndexProperties = null) where TAttribute : IndexKeyAttribute =>
        GetIndexOrdinals(indexType).Select((ordinal, i) => GetIndexKeyProperty<TAttribute>(type, indexType, ordinal, required: relatedIndexProperties?[i] is not null)).ToArray();

    static MemberInfo? ValidSingleResolvedPropertyResult(Type type, MemberInfo[] properties, string attributeDescription, bool required = false)
    {
        if (properties.Length > 1)
            throw new InvalidOperationException($"Expected at most one property with a {attributeDescription} attribute for {type.FullName}, got {properties.Length}");

        if (properties.Length == 0)
        {
            if (required)
                throw new InvalidOperationException($"Expected a property with a {attributeDescription} attribute for {type.FullName}");

            return null;
        }

        return properties[0];
    }

    static void FallBackToPrimaryPartitionKey(MemberInfo?[] indexPartitionKeyProperties, MemberInfo?[] indexSortKeyProperties, MemberInfo primaryPartitionKey)
    {
        for (var i = 0; i < indexPartitionKeyProperties.Length; i++)
        {
            if (indexPartitionKeyProperties[i] == null && indexSortKeyProperties[i] != null)
                indexPartitionKeyProperties[i] = primaryPartitionKey;
        }
    }

    static string ApplyTableNamePrefixAndMapping(DynamoDBClientOptions? options, string tableName) => 
        options == null
            ? tableName
            : options.TableNameMappings.TryGetValue(tableName, out var mappedName)
                ? mappedName
                : options.TableNamePrefix + tableName;


    internal static class Properties<T>
    {
        public static readonly (Type DeclaringType, string Name) PartitionKey = Get(typeof(T)).PartitionKeyProperty.AsSimplePropertyReference();
        
        public static readonly (Type DeclaringType, string Name)? SortKey = Get(typeof(T)).SortKeyProperty?.AsSimplePropertyReference();

        public static readonly (Type DeclaringType, string Name)? Version = Get(typeof(T)).VersionProperty?.AsSimplePropertyReference();
    }    

    internal static class PropertyTypes<T>
    {
        public static readonly Type PartitionKey = Get(typeof(T)).PartitionKeyProperty.GetPropertyType();
        
        public static readonly Type? SortKey = Get(typeof(T)).SortKeyProperty?.GetPropertyType();

        public static readonly Type? Version = Get(typeof(T)).VersionProperty?.GetPropertyType();
    }

    internal static class PropertyAccessors<T>
    {
        public static readonly Func<T, object?> GetPartitionKey = Get(typeof(T)).PartitionKeyProperty.CompilePropertyGetter<T, object?>();
        
        public static readonly Func<T, object?>? GetSortKey = Get(typeof(T)).SortKeyProperty?.CompilePropertyGetter<T, object?>();
        
        public static readonly Func<T, object?>? GetVersion = Get(typeof(T)).VersionProperty?.CompilePropertyGetter<T, object?>();
    }

    static class TableRequests
    {
        public static CreateTableRequest CreateTable(
            TableDescription table,
            IDynamoDBSerializer serializer,
            DynamoDBClientOptions? options = null,
            ProvisionedThroughput? provisionedThroughput = null, 
            Projection? projection = null, 
            StreamSpecification? streamSpecification = null,
            Func<Type, ScalarAttributeType>? mapToKeyAttributeType = null) =>
            new()
            {
                TableName = ApplyTableNamePrefixAndMapping(options, table.TableName),
                KeySchema = GetKeySchema(serializer, table.PartitionKeyProperty, table.SortKeyProperty),
                ProvisionedThroughput = provisionedThroughput ?? GetDefaultProvisionedThrougput(),
                StreamSpecification = streamSpecification ?? GetDefaultStreamSpecification(),
                SSESpecification = new SSESpecification { Enabled = true },
                AttributeDefinitions = (
                    from property in 
                        new[] { table.PartitionKeyProperty, table.SortKeyProperty }
                        .Concat(table.LocalSecondaryIndexSortKeyProperties)
                        .Concat(table.GlobalSecondaryIndexPartitionKeyProperties)
                        .Concat(table.GlobalSecondaryIndexSortKeyProperties)
                    where property != null
                    group property by serializer.GetPropertyAttributeInfo(property).AttributeName into propertiesPerName
                    let property = propertiesPerName.First()
                    select new AttributeDefinition
                    {
                        AttributeName = propertiesPerName.Key,
                        AttributeType = 
                            mapToKeyAttributeType?.Invoke(property.GetPropertyType()) ?? 
                            MapToScalarAttributeType(property.GetPropertyType())
                    })
                    .ToList(),

                LocalSecondaryIndexes = (
                    from ordinal in GetIndexOrdinals(IndexType.LocalSecondaryIndex)
                    let sortKey = table.LocalSecondaryIndexSortKeyProperties[ordinal]
                    where sortKey != null
                    select new LocalSecondaryIndex
                    {
                        IndexName = table.GetLocalSecondaryIndexName(ordinal),
                        KeySchema = GetKeySchema(serializer, table.PartitionKeyProperty, sortKey),
                        Projection = projection ?? GetDefaultProjection()
                    })
                    .ToList(),

                GlobalSecondaryIndexes = (
                    from ordinal in GetIndexOrdinals(IndexType.GlobalSecondaryIndex)
                    let partitionKey = table.GlobalSecondaryIndexPartitionKeyProperties[ordinal]
                    let sortKey = table.GlobalSecondaryIndexSortKeyProperties[ordinal]
                    where partitionKey != null
                    select new GlobalSecondaryIndex
                    {
                        IndexName = table.GetGlobalSecondaryIndexName(ordinal),
                        KeySchema = GetKeySchema(serializer, partitionKey, sortKey),
                        Projection = projection ?? GetDefaultProjection(),
                        ProvisionedThroughput = provisionedThroughput ?? GetDefaultProvisionedThrougput()
                    })
                    .ToList(),
            };

        public static UpdateTableRequest UpdateTableProvisionedThroughput(
            TableDescription table,
            DynamoDBClientOptions? options = null,
            int? readCapacityUnits = null,
            int? writeCapacityUnits = null) =>
            new()
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
                    })
                    .ToList(),
            };


        static List<KeySchemaElement> GetKeySchema(IDynamoDBSerializer serializer, MemberInfo partitionKeyProperty, MemberInfo? sortKeyProperty)
        {
            var elements =
                new List<KeySchemaElement>
                {
                    new() 
                    {
                        AttributeName = serializer.GetPropertyAttributeInfo(partitionKeyProperty).AttributeName,
                        KeyType = KeyType.HASH
                    }
                };

            if (sortKeyProperty != null)
            {
                elements.Add(
                    new()
                    {
                        AttributeName = serializer.GetPropertyAttributeInfo(sortKeyProperty).AttributeName,
                        KeyType = KeyType.RANGE
                    });
            }

            return elements;
        }

        static ProvisionedThroughput GetDefaultProvisionedThrougput() =>
            new()
            {
                ReadCapacityUnits = DefaultReadCapacityUnits,
                WriteCapacityUnits = DefaultWriteCapacityUnits
            };

        static Projection GetDefaultProjection() =>
            new()
            {
                ProjectionType = ProjectionType.ALL
            };

        static StreamSpecification GetDefaultStreamSpecification() =>
            new()
            {
                StreamEnabled = true,
                StreamViewType = StreamViewType.NEW_AND_OLD_IMAGES
            };

    
        static ScalarAttributeType MapToScalarAttributeType(Type type)
        {
            type = type.UnwrapNullableType();

            return Type.GetTypeCode(type) switch
            {
                TypeCode.SByte or 
                TypeCode.Byte or 
                TypeCode.Int16 or 
                TypeCode.UInt16 or 
                TypeCode.Int32 or 
                TypeCode.UInt32 or 
                TypeCode.Int64 or 
                TypeCode.UInt64 or 
                TypeCode.Single or 
                TypeCode.Double or 
                TypeCode.Decimal =>
                    ScalarAttributeType.N,

                _ => 
                    type == typeof(byte[])
                        ? ScalarAttributeType.B
                        : ScalarAttributeType.S
            };
        }
    }
}
