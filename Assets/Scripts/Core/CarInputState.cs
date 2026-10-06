using System;
using UnityEngine;
using Unity.Netcode;

namespace RacingMobile.Core
{
    /// <summary>
    /// Represents the instantaneous input applied to a racing vehicle.
    /// Implements INetworkSerializable for Netcode client-to-server input streaming.
    /// </summary>
    [Serializable]
    public struct CarInputState : INetworkSerializable
    {
        [Range(-1f, 1f)]
        public float Steer;       // -1 = Full Left, 0 = Center, +1 = Full Right

        [Range(0f, 1f)]
        public float Throttle;    // 0 = Idle, 1 = Full Acceleration

        [Range(0f, 1f)]
        public float Brake;       // 0 = None, 1 = Full Braking (or Reverse when stopped)

        public bool Handbrake;    // Handbrake / Drift activation

        public bool Boost;        // Nitrous / Turbo boost

        public static CarInputState Empty => new CarInputState();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Steer);
            serializer.SerializeValue(ref Throttle);
            serializer.SerializeValue(ref Brake);
            serializer.SerializeValue(ref Handbrake);
            serializer.SerializeValue(ref Boost);
        }
    }

    /// <summary>
    /// High-performance network snapshot of vehicle physical state.
    /// Transmitted via Netcode for GameObjects NetworkVariable or RPC.
    /// </summary>
    [Serializable]
    public struct CarNetworkSnapshot : INetworkSerializable, IEquatable<CarNetworkSnapshot>
    {
        public double Timestamp;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public float SteerAngle;
        public float WheelRPM;
        public float SpeedKmH;
        public bool Handbrake;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Timestamp);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref AngularVelocity);
            serializer.SerializeValue(ref SteerAngle);
            serializer.SerializeValue(ref WheelRPM);
            serializer.SerializeValue(ref SpeedKmH);
            serializer.SerializeValue(ref Handbrake);
        }

        public bool Equals(CarNetworkSnapshot other)
        {
            return Math.Abs(Timestamp - other.Timestamp) < 0.0001 &&
                   Position == other.Position &&
                   Rotation == other.Rotation &&
                   Mathf.Approximately(SteerAngle, other.SteerAngle) &&
                   Mathf.Approximately(SpeedKmH, other.SpeedKmH);
        }
    }

    /// <summary>
    /// Interface for any source providing input to the vehicle (Mobile UI, AI, Keyboard).
    /// </summary>
    public interface ICarInputProvider
    {
        CarInputState GetInput();
    }
}
