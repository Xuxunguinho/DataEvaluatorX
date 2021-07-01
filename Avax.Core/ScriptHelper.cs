﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using lizzie;
using lizzie.exceptions;
using Lex;

namespace Avax.Core
{
    /// <summary>
    /// implementing Lizzie for application
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ScriptHelper<T>
    {
        private static Func<IEnumerable<string>, object, double> Nota => (nota, x) => x.GetDynValue(nota).ToDouble();
        private static readonly Func<object, string> MaperFunc = (obj) => $"map({obj})";
        private static readonly Regex MapRegex = new Regex("map\\(('[^>]+',[a-zA-Z0-9]+)\\)");
        private readonly Binder<ScriptHelper<T>> _masterBinder;

        /// <summary>
        ///  class builder
        /// </summary>
        public ScriptHelper()
        {
            _masterBinder = new Binder<ScriptHelper<T>>();
            LambdaCompiler.BindFunctions(_masterBinder);
            AddBind("$nota", new string[] { });
            AddBind("$result", new string[] { });
            AddBind("$obs", new string[] { });
            var aValue = default(T);
            AddBind("$aluno", aValue);
            AddBind("notasAluno", new T[] { });
            AddBind("$pkAll", (Func<T, T, bool>) null);
        }

        /// <summary>
        ///  converts a dictionary into Lizzie's map object
        /// </summary>
        /// <param name="source">the dictionary</param>
        /// <returns></returns>
        public string MapFromDictionary(Dictionary<string, object> source)
        {
            try
            {
                var str = string.Empty;
                var i = 0;
                if (source is null) return string.Empty;
                if (source.Keys.Count < 1) return string.Empty;
                foreach (var key in source.Keys)
                {
                    str += i == 0 ? source.ValueFromKey(key) : "," + source.ValueFromKey(key);
                    i++;
                }

                return MaperFunc(str);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /// <summary>
        ///  converts a Lizzie's map object to dictionary
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Dictionary<string, object> DictionaryFormMap(string code)
        {
            try
            {
                if (!MapRegex.IsMatch(code))
                {
                    throw new Exception("o codigo naão se encontra no formato corecto");
                }

                var executeMapa = LambdaCompiler.Compile<ScriptHelper<T>>(new ScriptHelper<T>(), _masterBinder, code);
                var result = executeMapa() as Dictionary<string, object>;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /// <summary>
        /// set binders for Lizzie
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void AddBind(string name, object value)
        {
            _masterBinder[name] = value;
        }

        /// <summary>
        /// assigns value to an existing Bind
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public object SetValueForBind(string name, object value) => _masterBinder[name] = value;

        /// <summary>
        ///  run Lizzie Script
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public object Run(string code)
        {
            var lambda = LambdaCompiler.Compile<ScriptHelper<T>>(new ScriptHelper<T>(), _masterBinder, code);
            return lambda();
        }

        /// <summary>
        ///  adds grade classes to be recognized as part of Lizzie's syntax and extracts datasets for each class
        /// </summary>
        /// <param name="map">grade classes </param>
        /// <param name="av">set to be evaluated</param>
        /// <param name="field">field to be evaluated</param>
        /// <returns></returns>
        public Dictionary<string, List<T>> ClasseNotasBinder(Dictionary<string, object> map, T[] av,
            IEnumerable<string> field)
        {
            var dictionary = new Dictionary<string, List<T>>();
            if (map?.Keys == null) return new Dictionary<string, List<T>>();
            foreach (var key in map?.Keys)
            {
                var symbolName = key + "s";
                var expr = map[key].ToString().SupressSpace();
                var exec = Run(expr) as Function<ScriptHelper<T>>;
                var data = av.Where(x =>
                        exec != null && (bool) exec(this, _masterBinder, new Arguments {Nota(field, x)}))
                    ?.ToList();
                _masterBinder[symbolName] = data;
                dictionary.Add(symbolName, data);
            }

            AddBind("result_map", dictionary);
            return dictionary;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        public string ResultBinder(Dictionary<string, object> map)
        {
            const string code = @"if(@,{$R->(#)},{*})";
            if (map?.Keys == null) return string.Empty;

            const string str = code;
            foreach (var key in map?.Keys)
            {
                var symbolName = "is_" + key;
                var exec = Run(map[key].ToString().SupressSpace());
                var lambda = (bool) exec;
                _masterBinder[symbolName] = lambda;
                //str = str.Replace("@", symbolName).Replace("#",$"'{key}'").Replace("*", i < map.Keys.Count - 1 ? code : "escreva('')");
            }

            return str;
        }

        public bool Start()
        {
            return true;
        }

        #region extending Lizzie

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="LizzieException"></exception>
        [Bind(Name = "=>")]
        private object Contains(Binder<ScriptHelper<T>> binder, Arguments args)
        {
            if (args.Count != 3)
                throw new LizzieException("o metodo não pode conter mais  nem menos do que 2 argumento");
            var list = args.Get(0);
            var equate = args.Get(1) as IEnumerable<object>;

            var objects = equate as object[] ?? (equate ?? Array.Empty<object>()).ToArray();
            for (var i = 0; i < objects.Count(); i++)
                if (i < objects.Count() - 1)
                    if (objects[i].GetType() != objects[i + 1].GetType())
                        throw new LizzieException("os paramentros da lista de compracao devem ser do mesmo tipo");

            var field = args.Get(2) as string[];
            if (!(list is IEnumerable<T> source)) return false;
            var enumerable = source as T[] ?? source.ToArray();
            if (enumerable.IsNullOrEmpty()) return false;
            return !(equate is null) && equate.Any(x => enumerable.Count(z => z.GetDynValue(field).Compare(x)) > 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="LizzieException"></exception>
        [Bind(Name = "!=>")]
        private object NotContains(Binder<ScriptHelper<T>> binder, Arguments args)
        {
            if (args.Count != 3)
                throw new LizzieException("O método não pode conter mais nem menos do que 2 argumentos");

            var list = args.Get(0);
            var equate = args.Get(1) as IEnumerable<object>;
            var objects = equate as object[] ?? (equate ?? Array.Empty<object>()).ToArray();
           
            for (var i = 0; i < objects.Count(); i++)
                if (i < objects.Count() - 1)
                    if (objects[i].GetType() != objects[i + 1].GetType())
                        throw new LizzieException("Os paramentros da lista de compração devem ser do mesmo tipo");

            var field = args.Get(2) as string[];
            var teg = default(T);
            if(objects[0].GetType() != teg.GetFieldType(field))
                     throw new Exception("o campo de referência da função { !=>(conjunto,comparacao,referencia) } deve ser do memo tipo que itens do conjunto de comparação");
             

            if (!(list is IEnumerable<T> source)) return true;
            var enumerable = source as T[] ?? source.ToArray();
            if (enumerable.IsNullOrEmpty()) return true;
           
            if (equate is null) 
                throw new LizzieException("A lista de comparadores não pode ser nula");
            
            var listR = equate.Select(x => enumerable.Count(z => z.GetDynValue(field).Compare(x)) == 0).ToList();

            return (listR.Contains(true) && !listR.Contains(false)) || (!listR.Contains(true) && listR.Contains(false));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Bind(Name = "$R->")]
        private object Result(Binder<ScriptHelper<T>> binder, Arguments args)
        {
            if (args.Count > 1 || args.Count < 1)
                throw new Exception("Esta funcao nao pode ter mais nem menos do que 1 argumento");
            if (!(args.Get(0) is string result))
                throw new Exception("o argumento tem de ser uma string");

            var av = binder["notasAluno"] as T[];
            var storedData = binder["result_map"] as Dictionary<string, List<T>>;
            var aluno = (av ?? Array.Empty<T>()).FirstOrDefault();
            var nota = binder["$nota"] as string[];
            var discName = binder["discName"] as string[];
            var exprObs = binder["$obs"] as string[];
            var exprResult = binder["$result"] as string[];
            var pkeyAll = binder["$pkAll"] as Func<T, T, bool>;
            var obs = new StringBuilder();
            obs.Clear();
            obs.AppendLine();
            obs.AppendLine($"    Resultado -> {result}");
            if (storedData != null)
                foreach (var key in storedData.Keys)
                {
                    var collection = storedData[key];
                    var colet = DiscShow(collection, nota, discName);
                    obs.AppendLine($"    {key} -> {collection.Count} {colet}");
                }

            obs.AppendLine();
            av.Update(p =>
            {
                p.SetDynValue(obs.ToString(), exprObs);
                p.SetDynValue(result, exprResult);
            }, n => pkeyAll != null && pkeyAll(n, aluno));

            return null;
        }

        private static string DiscShow(IEnumerable<T> pauta, IEnumerable<string> notaKey,
            IEnumerable<string> dicNameKeys)
        {
            var str = pauta.Aggregate(string.Empty,
                (current, x) => (current.IsNullOrEmpty() ? current : current + ",") +
                                x.GetDynValue(dicNameKeys) + $"({Nota(notaKey, x)})");
            return $"[ {str} ]";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="LizzieException"></exception>
        [Bind(Name = "&")]
        private object And(Binder<ScriptHelper<T>> binder, Arguments args)
        {
            if (args.Count < 2)
                throw new LizzieException("o metodo não pode conter menos do que 2 argumentos");

            var equate = args as IEnumerable<object>;
            var objects = equate as object[] ?? (equate ?? Array.Empty<object>()).ToArray();
            //
            for (var i = 0; i < objects.Count(); i++)
                if (i < objects.Count() - 1)
                    if (objects[i].GetType() != objects[i + 1].GetType())
                        throw new LizzieException("os paramentros da lista de compracao devem ser do mesmo tipo");

            return (args.Contains(true) && !args.Contains(false)) || (!args.Contains(true) && args.Contains(false));

            /*var xr = args.Aggregate(true, (current, x) => current && (bool) x);
            return xr;*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="LizzieException"></exception>
        [Bind(Name = "ou")]
        private object Or(Binder<ScriptHelper<T>> binder, Arguments args)
        {
            if (args.Count < 2)
                throw new LizzieException("o metodo não pode conter menos do que 2 argumentos");
            return args.Aggregate(false, (current, x) => current || (bool) x);
        }

        #endregion
    }
}