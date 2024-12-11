using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Data;
using Microsoft.Extensions.Logging;

namespace FMSoftlab.ClassBuilder
{

    public class TypeBuilderFieldInfo
    {
        public TypeBuilderFieldInfo()
        {
            IsNullable = false;
        }
        public string FieldName { get; set; }
        public Type FieldType { get; set; }
        public bool IsNullable { get; set; }
    }

    public interface IObjectBuilder
    {
        Type CreateNewType(string typeName, IEnumerable<TypeBuilderFieldInfo> fieldsInfo);
        void AssignData(object instance, IDataReader reader);
        void AssignData(object instance, IDictionary<string, object> data);
        object CreateNewObject(Type type);
        object CreateObjectAssignData(Type type, IDictionary<string, object> data);
        object CreateObjectAssignData(Type type, IDataReader reader);
    }
    public class FMClassBuilder : IObjectBuilder
    {
        private ILogger _log;

        public FMClassBuilder(ILogger log)
        {
            _log=log;
        }
        private TypeBuilder GetTypeBuilder(string typeName)
        {
            var typeSignature = typeName;
            var an = new AssemblyName(typeSignature);
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
            TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                    TypeAttributes.Public |
                    TypeAttributes.Class |
                    TypeAttributes.AutoClass |
                    TypeAttributes.AnsiClass |
                    TypeAttributes.BeforeFieldInit |
                    TypeAttributes.AutoLayout,
                    null);
            return tb;
        }
        private Type CompileResultType(string typeName, IEnumerable<TypeBuilderFieldInfo> fieldsInfo)
        {
            TypeBuilder tb = GetTypeBuilder(typeName);
            ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

            // NOTE: assuming your list contains Field objects with fields FieldName(string) and FieldType(Type)
            foreach (var field in fieldsInfo)
            {
                if (field.IsNullable && field.FieldType!=typeof(string) && field.FieldType!=typeof(System.String))
                {
                    CreateNullableProperty(tb, field.FieldName, field.FieldType);
                }
                else
                {
                    CreateProperty(tb, field.FieldName, field.FieldType);
                }
            }
            //Type objectType = tb.CreateType();
            Type objectType = tb.CreateTypeInfo();
            return objectType;
        }
        public Type CreateNewType(string typeName, IEnumerable<TypeBuilderFieldInfo> fieldsInfo)
        {
            return CompileResultType(typeName, fieldsInfo);
        }
        public object CreateNewObject(Type type)
        {
            var myObject = Activator.CreateInstance(type);
            return myObject;
        }

        private void CreateNullableProperty(TypeBuilder tb, string propertyName, Type propertyType)
        {
            Type nullableType = typeof(Nullable<>).MakeGenericType(propertyType);

            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, nullableType, FieldAttributes.Private);

            //PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, nullableType, null);
            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.None, nullableType, Type.EmptyTypes);


            MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName,
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                nullableType, Type.EmptyTypes);

            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr = tb.DefineMethod("set_" + propertyName,
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                null, new[] { nullableType });

            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);

            // Check if the value type is nullable, if not, wrap it with Nullable<>
            /*if (propertyType.IsValueType && !propertyType.IsGenericType && propertyType != typeof(void))
            {
                setIl.Emit(OpCodes.Ldarg_1);
                setIl.Emit(OpCodes.Newobj, nullableType.GetConstructor(new[] { propertyType }));
            }
            else
            {
                setIl.Emit(OpCodes.Ldarg_0);
                setIl.Emit(OpCodes.Ldarg_1);
            }*/
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);
            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);
        }

        private void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
        {
            FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

            PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr =
                tb.DefineMethod("set_" + propertyName,
                  MethodAttributes.Public |
                  MethodAttributes.SpecialName |
                  MethodAttributes.HideBySig,
                  null, new[] { propertyType });

            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);
        }
        public object CreateObjectAssignData(Type type, IDataReader reader)
        {
            object res = CreateNewObject(type);
            AssignData(res, reader);
            return res;
        }
        public void AssignData(object instance, IDataReader reader)
        {
            if (instance == null)
                return;
            if (reader == null)
                return;
            foreach (PropertyInfo prop in instance.GetType().GetProperties())
            {
                if (!object.Equals(reader[prop.Name], DBNull.Value))
                {
                    object value = reader[prop.Name];
                    //log.Debug($"property:{prop.Name} value:{value}");
                    prop.SetValue(instance, value, null);
                }
            }
        }

        public object CreateObjectAssignData(Type type, IDictionary<string, object> data)
        {
            object res = CreateNewObject(type);
            AssignData(res, data);
            return res;
        }
        public void AssignData(object instance, IDictionary<string, object> data)
        {
            if (instance == null)
                return;
            if (data == null)
                return;
            foreach (PropertyInfo prop in instance.GetType().GetProperties())
            {
                if (data.TryGetValue(prop.Name, out object value))
                {
                    prop.SetValue(instance, value, null);
                }
            }
        }
    }
}