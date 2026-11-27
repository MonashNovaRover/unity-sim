// ROS-TCP-Connector message definitions for rcl_interfaces/srv/GetParameters
using System;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.RclInterfaces
{
    // rcl_interfaces/msg/ParameterValue
    [Serializable]
    public class ParameterValueMsg : Message
    {
        public const string k_RosMessageName = "rcl_interfaces/ParameterValue";
        public override string RosMessageName => k_RosMessageName;

        // Possible values of the type field (from rcl_interfaces/msg/ParameterType)
        public const byte PARAMETER_NOT_SET = 0;
        public const byte PARAMETER_BOOL = 1;
        public const byte PARAMETER_INTEGER = 2;
        public const byte PARAMETER_DOUBLE = 3;
        public const byte PARAMETER_STRING = 4;
        public const byte PARAMETER_BYTE_ARRAY = 5;
        public const byte PARAMETER_BOOL_ARRAY = 6;
        public const byte PARAMETER_INTEGER_ARRAY = 7;
        public const byte PARAMETER_DOUBLE_ARRAY = 8;
        public const byte PARAMETER_STRING_ARRAY = 9;

        public byte type;
        public bool bool_value;
        public long integer_value;
        public double double_value;
        public string string_value;
        public byte[] byte_array_value;
        public bool[] bool_array_value;
        public long[] integer_array_value;
        public double[] double_array_value;
        public string[] string_array_value;

        public ParameterValueMsg()
        {
            string_value = "";
            byte_array_value = new byte[0];
            bool_array_value = new bool[0];
            integer_array_value = new long[0];
            double_array_value = new double[0];
            string_array_value = new string[0];
        }

        public static ParameterValueMsg Deserialize(MessageDeserializer deserializer) => new ParameterValueMsg(deserializer);

        private ParameterValueMsg(MessageDeserializer deserializer)
        {
            deserializer.Read(out type);
            deserializer.Read(out bool_value);
            deserializer.Read(out integer_value);
            deserializer.Read(out double_value);
            deserializer.Read(out string_value);
            deserializer.Read(out byte_array_value, sizeof(byte), deserializer.ReadLength());
            deserializer.Read(out bool_array_value, sizeof(bool), deserializer.ReadLength());
            deserializer.Read(out integer_array_value, sizeof(long), deserializer.ReadLength());
            deserializer.Read(out double_array_value, sizeof(double), deserializer.ReadLength());
            deserializer.Read(out string_array_value, deserializer.ReadLength());
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(type);
            serializer.Write(bool_value);
            serializer.Write(integer_value);
            serializer.Write(double_value);
            serializer.Write(string_value);
            serializer.WriteLength(byte_array_value);
            serializer.Write(byte_array_value);
            serializer.WriteLength(bool_array_value);
            serializer.Write(bool_array_value);
            serializer.WriteLength(integer_array_value);
            serializer.Write(integer_array_value);
            serializer.WriteLength(double_array_value);
            serializer.Write(double_array_value);
            serializer.WriteLength(string_array_value);
            serializer.Write(string_array_value);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        public static void Register()
        {
            MessageRegistry.Register(k_RosMessageName, Deserialize);
        }
    }

    // rcl_interfaces/srv/GetParameters request
    [Serializable]
    public class GetParametersRequest : Message
    {
        public const string k_RosMessageName = "rcl_interfaces/GetParameters";
        public override string RosMessageName => k_RosMessageName;

        public string[] names;

        public GetParametersRequest()
        {
            names = new string[0];
        }

        public GetParametersRequest(string[] names)
        {
            this.names = names;
        }

        public static GetParametersRequest Deserialize(MessageDeserializer deserializer) => new GetParametersRequest(deserializer);

        private GetParametersRequest(MessageDeserializer deserializer)
        {
            deserializer.Read(out names, deserializer.ReadLength());
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.WriteLength(names);
            serializer.Write(names);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        public static void Register()
        {
            MessageRegistry.Register(k_RosMessageName, Deserialize);
        }
    }

    // rcl_interfaces/srv/GetParameters response
    [Serializable]
    public class GetParametersResponse : Message
    {
        public const string k_RosMessageName = "rcl_interfaces/GetParameters";
        public override string RosMessageName => k_RosMessageName;

        public ParameterValueMsg[] values;

        public GetParametersResponse()
        {
            values = new ParameterValueMsg[0];
        }

        public static GetParametersResponse Deserialize(MessageDeserializer deserializer) => new GetParametersResponse(deserializer);

        private GetParametersResponse(MessageDeserializer deserializer)
        {
            deserializer.Read(out values, ParameterValueMsg.Deserialize, deserializer.ReadLength());
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.WriteLength(values);
            serializer.Write(values);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        public static void Register()
        {
            MessageRegistry.Register(k_RosMessageName, Deserialize, MessageSubtopic.Response);
        }
    }
}
