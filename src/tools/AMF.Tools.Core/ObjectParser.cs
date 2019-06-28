using System;
using System.Collections.Generic;
using System.Linq;
using AMF.Api.Core;
using AMF.Tools.Core.XML;
using AMF.Parser.Model;

namespace AMF.Tools.Core
{
    public class ObjectParser
    {
        //private readonly JsonSchemaParser jsonSchemaParser = new JsonSchemaParser();
        private IDictionary<string, ApiObject> emptyDic = new Dictionary<string, ApiObject>();
        private IDictionary<string, ApiObject> newObjects = new Dictionary<string, ApiObject>();
        private IDictionary<string, ApiEnum> newEnums = new Dictionary<string, ApiEnum>();
        private IDictionary<string, ApiObject> existingObjects;
        private IDictionary<string, ApiEnum> existingEnums;
        private IDictionary<string, string> warnings;

        public Tuple<IDictionary<string, ApiObject>,IDictionary<string, ApiEnum>, IDictionary<Guid, string>> ParseObject(Guid id, string key, Shape shape, 
            IDictionary<string, ApiObject> existingObjects, IDictionary<string, string> warnings, IDictionary<string, ApiEnum> existingEnums, 
            bool isRootType = false)
        {
            this.existingObjects = existingObjects;
            this.existingEnums = existingEnums;
            this.warnings = warnings;

            if (shape is ScalarShape scalar && scalar.Values != null && scalar.Values.Any())
                return ParseEnum(id, warnings, existingEnums, scalar);

            return ParseObject(id, key, shape, existingObjects, isRootType);
        }

        private Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>> ParseEnum(Guid id, IDictionary<string, string> warnings, 
            IDictionary<string, ApiEnum> existingEnums, ScalarShape scalar)
        {
            var linkedTypes = new Dictionary<Guid, string>();

            if (existingEnums.ContainsKey(scalar.Id))
                return new Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>>(newObjects, newEnums, linkedTypes);

            var apiEnum = ParseEnum(scalar, existingEnums, warnings, newEnums);
            apiEnum.Id = id;
            apiEnum.AmfId = scalar.Id;
            if(!newEnums.ContainsKey(apiEnum.AmfId))
                newEnums.Add(apiEnum.AmfId, apiEnum);

            return new Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>>(newObjects, newEnums, linkedTypes);
        }

        private Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>> ParseObject(Guid id, string key, Shape shape, 
            IDictionary<string, ApiObject> existingObjects, bool isRootType)
        {
            var linkedType = new Dictionary<Guid, string>();

            var apiObj = new ApiObject
            {
                Id = id,
                AmfId = shape.Id,
                BaseClass = GetBaseClass(shape),
                IsArray = shape is ArrayShape,
                IsScalar = shape is ScalarShape,
                IsUnionType = shape is UnionShape,
                Name = NetNamingMapper.GetObjectName(key),
                Type = NetNamingMapper.GetObjectName(key),
                Description = shape.Description,
                Example = MapExample(shape)
            };

            if (isRootType && (apiObj.IsArray || apiObj.IsScalar))
                apiObj.Type = NewNetTypeMapper.GetNetType(shape, existingObjects);

            if (shape is NodeShape node && node.Properties.Count() == 1 && node.Properties.First().Path != null && node.Properties.First().Path.StartsWith("/")
                && node.Properties.First().Path.EndsWith("/"))
            {
                apiObj.IsMap = true;
                var valueType = "object";
                if(node.Properties.First().Range != null)
                    valueType = NewNetTypeMapper.GetNetType(node.Properties.First().Range, existingObjects);

                apiObj.Type = $"Dictionary<string, {valueType}>";
                return new Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>>(newObjects, newEnums, linkedType);
            }

            apiObj.Properties = MapProperties(shape, apiObj.Name).ToList();

            ApiObject match = null;

            if (existingObjects.Values.Any(o => o.Name == apiObj.Name) || newObjects.Values.Any(o => o.Name == apiObj.Name))
            {
                if (UniquenessHelper.HasSameProperties(apiObj, existingObjects, key, newObjects, emptyDic))
                    return new Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>>(newObjects, newEnums, linkedType);

                match = UniquenessHelper.FirstOrDefaultWithSameProperties(apiObj, existingObjects, key, newObjects, emptyDic);
                if (match != null)
                {
                    linkedType.Add(id, match.Type);
                }
                else
                {
                    apiObj.Name = UniquenessHelper.GetUniqueName(existingObjects, apiObj.Name, newObjects, emptyDic);
                    foreach (var prop in apiObj.Properties)
                    {
                        prop.ParentClassName = apiObj.Name;
                    }
                }
            }
            if (existingObjects.Values.Any(o => o.Type == apiObj.Type) || newObjects.Values.Any(o => o.Type == apiObj.Type))
            {
                if (UniquenessHelper.HasSameProperties(apiObj, existingObjects, key, newObjects, emptyDic))
                    return new Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>>(newObjects, newEnums, linkedType);

                match = UniquenessHelper.FirstOrDefaultWithSameProperties(apiObj, existingObjects, key, newObjects, emptyDic);
                if (match != null)
                {
                    if(!linkedType.ContainsKey(id))
                        linkedType.Add(id, match.Type);
                }
                else
                {
                    apiObj.Type = UniquenessHelper.GetUniqueName(existingObjects, apiObj.Type, newObjects, emptyDic);
                }
            }

            if (match == null)
            {
                match = UniquenessHelper.FirstOrDefaultWithSameProperties(apiObj, existingObjects, key, newObjects, emptyDic);
                if (match != null)
                {
                    if (!linkedType.ContainsKey(id))
                        linkedType.Add(id, match.Type);
                }
            }


            if (shape.Inherits != null && shape.Inherits.Count() == 1)
            {
                var baseClass = NewNetTypeMapper.GetNetType(shape.Inherits.First(), existingObjects, newObjects, existingEnums, newEnums);
                if(!string.IsNullOrWhiteSpace(baseClass))
                    apiObj.BaseClass = CollectionTypeHelper.GetConcreteType(baseClass);
            }

            var amfId = shape.Id;
            if (string.IsNullOrWhiteSpace(amfId))
                amfId = apiObj.Name;

            if (!newObjects.ContainsKey(amfId))
                newObjects.Add(amfId, apiObj);

            return new Tuple<IDictionary<string, ApiObject>, IDictionary<string, ApiEnum>, IDictionary<Guid, string>>(newObjects, newEnums, linkedType);
        }

        //TODO: check
        private string GetBaseClass(Shape shape)
        {
            if (shape.Inherits.Count() == 0)
                return null;

            if (shape.Inherits.Count() > 1) // no multiple inheritance in c#
                return null;

            if (shape is NodeShape node && node.Properties.Count() == 0) // has no extra properties, so its the same type...
                return null;

            return shape.Inherits.First().Name;
        }

        public static string MapExample(Shape shape)
        {
            if (shape is AnyShape)
                return string.Join("\r\n", ((AnyShape)shape).Examples.Select(e => e.Value));

            return null;
        }

        private IEnumerable<Property> MapProperties(Shape shape, string parentClassName)
        {
            if(shape is NodeShape)
            {
                return ((NodeShape)shape).Properties.Where(p => p != null)
                    .Select(p => MapProperty(p, parentClassName)).ToArray();
            }

            return new Property[0];
        }

        private string GetPropertyName(PropertyShape p)
        {
            if (p.Path != null)
                return GetNameFromPath(p.Path);

            if(p.Range == null)
                return "prop" + DateTime.Now.Ticks.ToString();

            if(!string.IsNullOrWhiteSpace(p.Range.Name))
                return p.Range.Name;

            return GetNameFromPath(p.Range.Id);
        }

        private Property MapProperty(PropertyShape p, string parentClassName)
        {
            var name = GetPropertyName(p);
            var prop = new Property(parentClassName)
            {
                Name = NetNamingMapper.GetObjectName(name),
                Required = p.Required,
                Type = NetNamingMapper.GetObjectName(name)
            };

            if (p.Range == null)
                return prop;

            prop.AmfId = p.Range.Id;
            prop.Name = NetNamingMapper.GetObjectName(name);
            prop.Description = p.Range.Description;
            prop.Example = MapExample(p.Range);
            prop.Type = NewNetTypeMapper.GetNetType(p.Range, existingObjects, newObjects, existingEnums, newEnums);

            if (p.Range is ScalarShape scalar)
            {
                prop.Pattern = scalar.Pattern;
                prop.MaxLength = scalar.MaxLength;
                prop.MinLength = scalar.MinLength;
                prop.Maximum = string.IsNullOrWhiteSpace(scalar.Maximum) ? (double?)null : Convert.ToDouble(scalar.Maximum);
                prop.Minimum = string.IsNullOrWhiteSpace(scalar.Minimum) ? (double?)null : Convert.ToDouble(scalar.Minimum);
                if(scalar.Values != null && scalar.Values.Any())
                {
                    prop.IsEnum = true;

                    var apiEnum = ParseEnum(scalar, existingEnums, warnings, newEnums);

                    string amfId = apiEnum.AmfId;
                    if (string.IsNullOrWhiteSpace(amfId))
                        amfId = prop.AmfId ?? prop.Name;

                    if (!newEnums.ContainsKey(amfId))
                        newEnums.Add(amfId, apiEnum);

                    prop.Type = apiEnum.Name;
                }
                return prop;
            }

            //if (existingObjects.ContainsKey(prop.Type) || newObjects.ContainsKey(prop.Type))
            //    return prop;

            if (p.Range is NodeShape)
            {
                var id = Guid.NewGuid();
                prop.TypeId = id;
                var tuple = ParseObject(id, prop.Name, p.Range, existingObjects, warnings, existingEnums);
                prop.Type = NetNamingMapper.GetObjectName(prop.Name);
            }
            if (p.Range is ArrayShape array)
            {
                if (array.Items is ScalarShape)
                    return prop;

                if (!string.IsNullOrWhiteSpace(array.Items.LinkTargetName))
                    return prop;

                var itemType = NewNetTypeMapper.GetNetType(array.Items, existingObjects, newObjects, existingEnums, newEnums);

                GetCollectionType(prop, itemType);

                if (array.Items is NodeShape && itemType == "Items")
                {
                    itemType = NetNamingMapper.GetObjectName(array.Name);
                    prop.Type = CollectionTypeHelper.GetCollectionType(NetNamingMapper.GetObjectName(array.Name));
                }

                if (existingObjects.ContainsKey(array.Id))
                    return prop;

                var newId = Guid.NewGuid();
                prop.TypeId = newId;
                ParseObject(newId, array.Name, array.Items, existingObjects, warnings, existingEnums);
            }

            foreach (var parent in p.Range.Inherits)
            {
                if (!(parent is ScalarShape) && !NewNetTypeMapper.IsPrimitiveType(prop.Type)
                    && !(CollectionTypeHelper.IsCollection(prop.Type) && NewNetTypeMapper.IsPrimitiveType(CollectionTypeHelper.GetBaseType(prop.Type)))
                    && string.IsNullOrWhiteSpace(parent.LinkTargetName))
                {
                    var newId = Guid.NewGuid();
                    ParseObject(newId, prop.Name, parent, existingObjects, warnings, existingEnums);
                }
            }
            return prop;
        }

        private static string GetCollectionType(Property prop, string itemType)
        {
            if (NewNetTypeMapper.IsPrimitiveType(itemType))
                return CollectionTypeHelper.GetCollectionType(itemType);

            if (CollectionTypeHelper.IsCollection(itemType))
                return CollectionTypeHelper.GetCollectionType(itemType);

            return CollectionTypeHelper.GetCollectionType(NetNamingMapper.GetObjectName(itemType));
        }

        private ApiEnum ParseEnum(ScalarShape scalar, IDictionary<string, ApiEnum> existingEnums, IDictionary<string, string> warnings, IDictionary<string, ApiEnum> newEnums)
        {
            return new ApiEnum
            {
                Name = NetNamingMapper.GetObjectName(scalar.Name),
                Description = scalar.Description,
                Values = scalar.Values.Select(p => new PropertyBase { Name = NetNamingMapper.GetEnumValueName(p), OriginalName = p }).ToArray()
            };
        }

        private string GetNameFromPath(string path)
        {
            return path.Substring(path.LastIndexOf("#") + 1);
        }

        //public static string MapShapeType(Shape shape) //TODO: check
        //{
        //    if (!string.IsNullOrWhiteSpace(shape.LinkTargetName))
        //        return shape.LinkTargetName;

        //    if (shape is ScalarShape)
        //        return ((ScalarShape)shape).DataType;

        //    if (shape is ArrayShape)
        //        return MapShapeType(((ArrayShape)shape).Items) + "[]";

        //    if (shape is FileShape)
        //    {
        //        return "file";
        //    }

        //    if (shape.Inherits.Count() == 1)
        //    {
        //        if (shape is NodeShape nodeShape && nodeShape.Properties.Count() == 0)
        //            return MapShapeType(nodeShape.Inherits.First());

        //        if (shape.Inherits.First() is ArrayShape arrayShape)
        //            return MapShapeType(arrayShape.Items) + "[]";
        //    }
        //    if (shape.Inherits.Count() > 0)
        //    {
        //        //TODO: check
        //    }

        //    return shape.Name;
        //}

        //public ApiObject ParseObject(string key, string value, IDictionary<string, ApiObject> objects, IDictionary<string, string> warnings, IDictionary<string, ApiEnum> enums, IDictionary<string, ApiObject> otherObjects, IDictionary<string, ApiObject> schemaObjects, string targetNamespace)
        //{
        //    var obj = ParseSchema(key, value, objects, warnings, enums, otherObjects, schemaObjects, targetNamespace);
        //    if (obj == null)
        //        return null;

        //    obj.Name = NetNamingMapper.GetObjectName(key);

        //    if (schemaObjects.Values.Any(o => o.Name == obj.Name) || objects.Values.Any(o => o.Name == obj.Name) ||
        //        otherObjects.Values.Any(o => o.Name == obj.Name))
        //    {
        //        if (UniquenessHelper.HasSameProperties(obj, objects, key, otherObjects, schemaObjects))
        //            return null;

        //        obj.Name = UniquenessHelper.GetUniqueName(objects, obj.Name, otherObjects, schemaObjects);
        //    }

        //    return obj;
        //}

  //      private ApiObject ParseSchema(string key, string schema, IDictionary<string, ApiObject> objects, IDictionary<string, string> warnings, 
  //          IDictionary<string, ApiEnum> enums, IDictionary<string, ApiObject> otherObjects, IDictionary<string, ApiObject> schemaObjects, string modelsNamespace)
		//{
  // 			if (schema == null)
		//		return null;

  //          // is a reference, should then be defined elsewhere
  //          if (schema.Contains("<<") && schema.Contains(">>"))
  //              return null;

  //          if (schema.Trim().StartsWith("<"))
  //              return ParseXmlSchema(key, schema, objects, modelsNamespace, otherObjects, schemaObjects);

  //          if (!schema.Contains("{"))
  //              return null;

  //          // return jsonSchemaParser.Parse(key, schema, objects, warnings, enums, otherObjects, schemaObjects);
  //          return null;
  //      }

  //      private ApiObject ParseXmlSchema(string key, string schema, IDictionary<string, ApiObject> objects, string modelsNamespace, IDictionary<string, ApiObject> otherObjects, IDictionary<string, ApiObject> schemaObjects)
		//{
  //          if(objects.ContainsKey(key))
  //              return null;

		//    var xmlSchemaParser = new XmlSchemaParser();
  //          var  obj = xmlSchemaParser.Parse(key, schema, objects, modelsNamespace);

		//    if (obj != null && !objects.ContainsKey(key) && !UniquenessHelper.HasSameProperties(obj, objects, otherObjects, schemaObjects))
		//        objects.Add(key, obj); // to associate that key with the main XML Schema object

		//    return obj;
		//}

    }
}