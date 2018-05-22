using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AMF.Parser.Model;
using AMF.Tools.Core.Pluralization;
using AMF.Api.Core;

namespace AMF.Tools.Core
{
    public abstract class GeneratorServiceBase
    {
        private readonly ObjectParser objectParser = new ObjectParser();
        //private RamlTypeParser raml1TypesParser;

        protected readonly string[] suffixes = { "A", "B", "C", "D", "E", "F", "G" };

        protected readonly UriParametersGenerator uriParametersGenerator;
        protected readonly SchemaParameterParser schemaParameterParser = new SchemaParameterParser(new EnglishPluralizationService());
        protected IDictionary<string, ApiObject> schemaObjects = new Dictionary<string, ApiObject>();
        protected IDictionary<string, ApiObject> schemaRequestObjects = new Dictionary<string, ApiObject>();
        protected IDictionary<string, ApiObject> schemaResponseObjects = new Dictionary<string, ApiObject>();
        protected IDictionary<string, string> linkKeysWithObjectNames = new Dictionary<string, string>();

        protected readonly ApiObjectsCleaner apiObjectsCleaner;        

		protected IDictionary<string, string> warnings;
	    protected IDictionary<string, ApiEnum> enums;
		protected readonly AmfModel raml;
	    protected readonly string targetNamespace;
	    protected ICollection<string> classesNames;
		protected IDictionary<string, ApiObject> uriParameterObjects;
        
        public const string RequestContentSuffix = "RequestContent";
		public const string ResponseContentSuffix = "ResponseContent";

        public AmfModel ParsedContent { get { return raml; } }

        public IEnumerable<ApiEnum> Enums { get { return enums.Values; } }

		protected GeneratorServiceBase(AmfModel raml, string targetNamespace)
		{
			this.raml = raml;
		    this.targetNamespace = targetNamespace;
		    apiObjectsCleaner = new ApiObjectsCleaner(schemaRequestObjects, schemaResponseObjects, schemaObjects);
		    uriParametersGenerator = new UriParametersGenerator(schemaObjects);
		    //ApplyResourceTypesAndTraits(raml.WebApi.EndPoints);
            //raml1TypesParser = new RamlTypeParser(raml.Shapes, schemaObjects, targetNamespace, enums, warnings);
		}

        protected static string CalculateClassKey(string url)
        {
            return url.GetHashCode().ToString(CultureInfo.InvariantCulture);
        }

        protected static void GetInheritedUriParams(IDictionary<string, Parameter> parentUriParameters, Parser.Model.EndPoint resource)
        {
            foreach (var uriParam in resource.Parameters.Where(p => p.Binding == "URL")) //TODO: check
            {
                if (!parentUriParameters.ContainsKey(uriParam.Name))
                    parentUriParameters.Add(uriParam.Name, uriParam);
            }
        }

        private string GetResourceType(IDictionary<string, IDictionary<string, string>> type)
        {
            return type != null && type.Any() ? type.First().Key : string.Empty;
        }

        private void AddObjectToObjectCollectionOrLink(ApiObject obj, string key, IDictionary<string, ApiObject> objects, IDictionary<string, ApiObject> otherObjects)
        {
            if (obj == null || (!obj.Properties.Any() && obj.Type == null))
                return;

            if (schemaObjects.All(o => o.Value.Name != obj.Name) && objects.All(o => o.Value.Name != obj.Name) && otherObjects.All(o => o.Value.Name != obj.Name))
            {
                objects.Add(key, obj);
            }
            else
            {
                if (UniquenessHelper.HasSameProperties(obj, objects, key, otherObjects, schemaObjects))
                {
                    if (string.IsNullOrWhiteSpace(obj.GeneratedCode) && !linkKeysWithObjectNames.ContainsKey(key))
                        linkKeysWithObjectNames.Add(key, obj.Name);
                }
                else if(!objects.ContainsKey(key))
                {
                    obj.Name = UniquenessHelper.GetUniqueName(objects, obj.Name, schemaObjects, schemaObjects);
                    objects.Add(key, obj);
                }
            }
        }

        protected void CleanProperties(IDictionary<string, ApiObject> apiObjects)
        {
            var keys = apiObjects.Keys.ToList();
            var apiObjectsCount = keys.Count - 1;
            for (var i = apiObjectsCount; i >= 0; i--)
            {
                var apiObject = apiObjects[keys[i]];
                var count = apiObject.Properties.Count;
                for (var index = count - 1; index >= 0; index--)
                {
                    var prop = apiObject.Properties[index];
                    var type = prop.Type;
                    if (!string.IsNullOrWhiteSpace(type) && IsCollectionType(type))
                        type = CollectionTypeHelper.GetBaseType(type);

                    if(prop.IsAdditionalProperties)
                        continue;

                    if (!NewNetTypeMapper.IsPrimitiveType(type) && schemaResponseObjects.All(o => o.Value.Name != type) 
                        && schemaRequestObjects.All(o => o.Value.Name != type)
                        && enums.All(e => e.Value.Name != type)
                        && schemaObjects.All(o => o.Value.Name != type))
                        apiObject.Properties.Remove(prop);
                }
            }
        }

        private bool IsCollectionType(string type)
        {
            return type.EndsWith(">") && type.StartsWith(CollectionTypeHelper.CollectionType);
        }

        protected void ParseSchemas()
        {
            foreach (var shape in raml.Shapes)
            {
                if (shape == null)
                    continue;

                var key = shape.Name;

                var newObjects = objectParser.ParseObject(key, shape, schemaObjects, warnings, enums, targetNamespace);

                AddNewObjects(newObjects);
            }
        }

        protected void AddNewObjects(IDictionary<string, ApiObject> newObjects)
        {
            foreach (var newObj in newObjects)
            {
                if (schemaObjects.ContainsKey(newObj.Key))
                {
                    if (UniquenessHelper.HasSameProperties(schemaObjects[newObj.Key], schemaObjects, newObj.Key,
                        new Dictionary<string, ApiObject>(), new Dictionary<string, ApiObject>()))
                        continue;

                    var apiObject = schemaObjects[newObj.Key];
                    var oldName = apiObject.Name;
                    apiObject.Name = UniquenessHelper.GetUniqueName(schemaObjects, apiObject.Name, new Dictionary<string, ApiObject>(), new Dictionary<string, ApiObject>());
                    var newKey = UniquenessHelper.GetUniqueKey(schemaObjects, newObj.Key, new Dictionary<string, ApiObject>());
                    schemaObjects.Add(newKey, apiObject);
                    schemaObjects.Remove(schemaObjects.First(o => o.Key == newObj.Key));
                    foreach (var apiObj in schemaObjects)
                    {
                        foreach (var prop in apiObj.Value.Properties)
                        {
                            if (prop.Type == oldName)
                                prop.Type = apiObject.Name;
                        }
                    }
                }
                schemaObjects.Add(newObj.Key, newObj.Value);
            }
        }

        protected string GetUniqueObjectName(Parser.Model.EndPoint resource, Parser.Model.EndPoint parent)
        {
            string objectName;

            if (resource.Path.StartsWith("/{") && resource.Path.EndsWith("}"))
            {
                objectName = NetNamingMapper.Capitalize(GetObjectNameForParameter(resource));
            }
            else
            {
                if (resource.Path == "/")
                    objectName = "RootUrl";
                else
                    objectName = NetNamingMapper.GetObjectName(resource.Path);

                if (classesNames.Contains(objectName))
                    objectName = NetNamingMapper.Capitalize(GetObjectNameForParameter(resource));
            }

            if (string.IsNullOrWhiteSpace(objectName))
                throw new InvalidOperationException("object name is null for " + resource.Path);

            if (!classesNames.Contains(objectName))
                return objectName;

            if (parent == null || string.IsNullOrWhiteSpace(parent.Path))
                return GetUniqueObjectName(objectName);

            if (resource.Path.StartsWith("/{") && parent.Path.EndsWith("}"))
            {
                objectName = NetNamingMapper.Capitalize(GetObjectNameForParameter(parent)) + objectName;
            }
            else
            {
                objectName = NetNamingMapper.GetObjectName(parent.Path) + objectName;
                if (classesNames.Contains(objectName))
                    objectName = NetNamingMapper.Capitalize(GetObjectNameForParameter(parent));
            }

            if (string.IsNullOrWhiteSpace(objectName))
                throw new InvalidOperationException("object name is null for " + resource.Path);

            if (!classesNames.Contains(objectName))
                return objectName;

            return GetUniqueObjectName(objectName);
        }

        private string GetUniqueObjectName(string name)
        {
            for (var i = 0; i < 7; i++)
            {
                var unique = name + suffixes[i];
                if (!classesNames.Contains(unique))
                    return unique;
            }
            for (var i = 0; i < 100; i++)
            {
                var unique = name + i;
                if (!classesNames.Contains(unique))
                    return unique;
            }
            throw new InvalidOperationException("Could not find a unique name for object " + name);
        }

        private static string GetObjectNameForParameter(Parser.Model.EndPoint resource)
        {
            var relativeUri = resource.Path.Replace("{mediaTypeExtension}", string.Empty);
            var objectNameForParameter = relativeUri.Substring(1).Replace("{", string.Empty).Replace("}", string.Empty);
            objectNameForParameter = NetNamingMapper.GetObjectName(objectNameForParameter);
            return objectNameForParameter;
        }

    }
}