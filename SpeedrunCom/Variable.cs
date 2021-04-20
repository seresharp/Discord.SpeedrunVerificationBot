using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VerificationBot.SpeedrunCom
{
    public class Variable
    {
        private readonly JObject Data;

        private string _id;
        public string Id
            => _id ??= Data.TryGet()["id"]?.ToString() ?? string.Empty;

        private string _name;
        public string Name
            => _name ??= Data.TryGet()["name"]?.ToString() ?? string.Empty;

        private VariableScope? _scope; // global, full-game, all-levels, single-level
        public VariableScope Scope
            => _scope ??= Enum.TryParse(Data.TryGet()["scope"]?["type"]?.ToString(), out VariableScope parsed)
                ? parsed
                : VariableScope.Invalid;

        private string _categoryId;
        public string CategoryId
            => _categoryId ??= Data.TryGet()["category"]?.ToString() ?? string.Empty;

        private string _levelId;
        public string LevelId
            => _levelId ??= Data.TryGet()["scope"]?["level"]?.ToString() ?? string.Empty;

        // TODO
        private bool? _isMandatory;
        public bool IsMandatory;

        private bool? _isSubcategory;
        public bool IsSubcategory;

        private bool? _isUserDefined;
        public bool IsUserDefined;

        private bool? _valuesObsoleteEachOther;
        public bool ValuesObsoleteEachOther;

        public Variable(JObject data)
            => Data = data;
    }

    public enum VariableScope
    {
        Invalid,
        Global,
        FullGame,
        Levels,
        SingleLevel
    }

    public class VariableValue
    {
        public string Title { get; }
        public string Value { get; }
        public bool IsSubcategory { get; }

        internal VariableValue(string title, string value, bool isSubCategory)
        {
            Title = title;
            Value = value;
            IsSubcategory = isSubCategory;
        }
    }
}
