using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PolicyUtil
{

    [DataContract]
    public class UsageConfig
    {
        [DataMember(Name = "refs")]
        public string[] References { get; set; }

        [DataMember(Name = "usings")]
        public string[] Usings { get; set; }

        [DataMember(Name = "allowed-types")]
        public Dictionary<string, MemberRule> AllowedUsageTypes { get; set; }

        [DataMember(Name = "allowed-assemblies")]
        public Dictionary<string, string[]> AllowedUsageAssemblies { get; set; }

        [DataMember(Name = "allowed-return-types")]
        public string[] AllowedReturnTypes { get; set; }

        public static readonly UsageConfig Empty = new UsageConfig
        {
            AllowedUsageTypes = new Dictionary<string, MemberRule>(),
            AllowedUsageAssemblies = new Dictionary<string, string[]>(),
            AllowedReturnTypes = new string[0],
            Usings = new string[0],
            References = new string[0]
        };

        [DataContract]
        public class MemberRule
        {
            [DataMember(Name = "allow", EmitDefaultValue = false)]
            public string[] Allow { get; set; }

            [DataMember(Name = "deny", EmitDefaultValue = false)]
            public string[] Deny { get; set; }
        }
    }
}
