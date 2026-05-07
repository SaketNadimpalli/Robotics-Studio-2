// UR3eIKSolver.cs
// -----------------------------------------------------------------------------
// Analytical inverse kinematics for the Universal Robots UR3e.
//
// Usage (static, no MonoBehaviour needed):
//   bool ok = UR3eIKSolver.Solve(targetPos, targetRot, currentAngles,
//                                 out float[] jointAngles);
//
// Input:
//   targetPos     — EE position in ROBOT BASE frame (metres, ROS right-handed)
//   targetRot     — EE orientation in ROBOT BASE frame (quaternion)
//   currentAngles — current 6 joint angles in RADIANS (used for branch selection)
//
// Output:
//   jointAngles   — 6 joint angles in RADIANS, or null if unreachable
//   returns true  — a valid solution was found within joint limits
//
// Coordinate convention:
//   Everything here is in ROS/robot frame (right-handed, Z-up).
//   Conversion from Unity world space happens BEFORE calling this solver,
//   in UR3TeleopDriver.cs. Do not pass Unity-frame coordinates here.
//
// DH parameters verified against Universal Robots e-Series parameters page,
// UR3e row, confirmed May 2026:
//   d = { 0.15185, 0, 0, 0.13105, 0.08535, 0.0921 }
//   a = { 0, -0.24355, -0.2132, 0, 0, 0 }
//   α = { π/2, 0, 0, π/2, -π/2, 0 }
// -----------------------------------------------------------------------------

using UnityEngine;

public static class UR3eIKSolver
{
    // -------------------------------------------------------------------------
    // DH Parameters — UR3e (verified against UR spec, May 2026)
    // -------------------------------------------------------------------------
    static readonly float[] d = { 0.15185f, 0f, 0f, 0.13105f, 0.08535f, 0.0921f };
    static readonly float[] a = { 0f, -0.24355f, -0.2132f, 0f, 0f, 0f };
    static readonly float[] alpha = { Mathf.PI / 2f, 0f, 0f, Mathf.PI / 2f, -Mathf.PI / 2f, 0f };

    // -------------------------------------------------------------------------
    // Joint limits (radians) — UR3e hardware limits
    // -------------------------------------------------------------------------
    static readonly float[] jointMin = {
        -2f * Mathf.PI, -2f * Mathf.PI, -Mathf.PI,
        -2f * Mathf.PI, -2f * Mathf.PI, -2f * Mathf.PI
    };
    static readonly float[] jointMax = {
        2f * Mathf.PI,  2f * Mathf.PI,  Mathf.PI,
        2f * Mathf.PI,  2f * Mathf.PI,  2f * Mathf.PI
    };

    // Tolerance for near-singular / unreachable detection
    const float EPSILON = 1e-6f;

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------
    /// <summary>
    /// Solve IK for the UR3e.
    /// </summary>
    /// <param name="targetPos">EE position in robot base frame (metres)</param>
    /// <param name="targetRot">EE orientation in robot base frame</param>
    /// <param name="currentAngles">Current joint angles in radians (for branch selection)</param>
    /// <param name="jointAngles">Output: 6 joint angles in radians</param>
    /// <returns>True if a valid solution was found</returns>
    public static bool Solve(
        Vector3 targetPos,
        Quaternion targetRot,
        float[] currentAngles,
        out float[] jointAngles)
    {
        jointAngles = null;

        // Build the 4x4 target transform matrix from pos + rot
        Matrix4x4 T06 = MatFromPosRot(targetPos, targetRot);

        // --- Step 1: Solve θ1 (two solutions: shoulder left / shoulder right)
        // The wrist centre is at T06 * [0, 0, -d6, 1]^T
        Vector3 wristCentre = T06.MultiplyPoint3x4(new Vector3(0f, 0f, -d[5]));

        float px = wristCentre.x;
        float py = wristCentre.y;

        // θ1 from atan2 of wrist centre projected onto base XY plane
        float r_xy = Mathf.Sqrt(px * px + py * py);
        if (r_xy < EPSILON)
        {
            Debug.LogWarning("[UR3eIK] Wrist centre at base Z-axis — shoulder singularity");
            return false;
        }

        float phi1 = Mathf.Atan2(py, px);
        float ratio1 = d[3] / r_xy;
        if (Mathf.Abs(ratio1) > 1f)
        {
            Debug.LogWarning("[UR3eIK] Target unreachable — wrist offset exceeds reach");
            return false;
        }
        float psi1 = Mathf.Asin(ratio1);

        // Two shoulder solutions
        float theta1_A = phi1 + psi1 + Mathf.PI / 2f;
        float theta1_B = Mathf.PI - phi1 + psi1 + Mathf.PI / 2f; // shoulder-right

        // Pick the shoulder solution closest to current θ1
        float theta1 = PickClosest(currentAngles[0],
                                   NormaliseAngle(theta1_A),
                                   NormaliseAngle(theta1_B));

        // --- Step 2: Solve θ5 (wrist-up / wrist-down, two solutions each)
        float c1 = Mathf.Cos(theta1);
        float s1 = Mathf.Sin(theta1);

        // T06 column 3 (z-axis of EE) dotted with base frame
        // p = T06 * origin gives us a useful intermediate
        float p05x = T06.m03 - d[5] * T06.m02;
        float p05y = T06.m13 - d[5] * T06.m12;

        float theta5_num = (p05x * s1 - p05y * c1 - d[3]);
        float ratio5 = theta5_num / d[4];
        // clamp for numeric safety
        ratio5 = Mathf.Clamp(ratio5, -1f, 1f);

        float theta5_A = Mathf.Acos(ratio5);
        float theta5_B = -Mathf.Acos(ratio5);

        // --- Step 3: Solve θ6 (depends on θ1 and θ5)
        // For each θ5 candidate, compute θ6
        // Uses the relationship between T06 and T01^-1 * T06
        float[] solutions = TryBothTheta5(
            T06, theta1, theta5_A, theta5_B, currentAngles);

        if (solutions == null)
        {
            Debug.LogWarning("[UR3eIK] No valid solution found within joint limits");
            return false;
        }

        jointAngles = solutions;
        return true;
    }

    // -------------------------------------------------------------------------
    // Internal: try both θ5 branches, return best valid solution
    // -------------------------------------------------------------------------
    static float[] TryBothTheta5(
        Matrix4x4 T06,
        float theta1,
        float theta5_A, float theta5_B,
        float[] current)
    {
        float[] best = null;
        float bestCost = float.MaxValue;

        foreach (float theta5 in new[] { theta5_A, theta5_B })
        {
            float[] sol = SolveForTheta1And5(T06, theta1, theta5, current);
            if (sol == null) continue;

            float cost = JointChangeCost(current, sol);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = sol;
            }
        }

        return best;
    }

    // -------------------------------------------------------------------------
    // Internal: given θ1 and θ5, solve for θ6, θ2, θ3, θ4
    // -------------------------------------------------------------------------
    static float[] SolveForTheta1And5(
        Matrix4x4 T06,
        float theta1, float theta5,
        float[] current)
    {
        float c1 = Mathf.Cos(theta1);
        float s1 = Mathf.Sin(theta1);
        float s5 = Mathf.Sin(theta5);

        // --- θ6 ----------------------------------------------------------
        float theta6;
        if (Mathf.Abs(s5) < EPSILON)
        {
            // Wrist singularity — θ6 is indeterminate, keep current
            theta6 = current[5];
        }
        else
        {
            float lhs = (-T06.m01 * s1 + T06.m11 * c1) / s5;
            float rhs = (T06.m00 * s1 - T06.m10 * c1) / s5;
            theta6 = Mathf.Atan2(lhs, rhs);
        }

        // --- Build T01^-1 * T06 to extract the 2-3-4 subproblem ----------
        Matrix4x4 T01 = DHMatrix(alpha[0], a[0], d[0], theta1);
        Matrix4x4 T01_inv = T01.inverse;

        Matrix4x4 T56 = DHMatrix(alpha[5], a[5], d[5], theta6);
        Matrix4x4 T56_inv = T56.inverse;

        Matrix4x4 T45 = DHMatrix(alpha[4], a[4], d[4], theta5);
        Matrix4x4 T45_inv = T45.inverse;

        // T14 = T01_inv * T06 * T56_inv * T45_inv
        Matrix4x4 T14 = T01_inv * T06 * T56_inv * T45_inv;

        // --- θ3 (elbow up / elbow down) ----------------------------------
        // From the T14 position components and the 2-3 link lengths
        float p14x = T14.m03;
        float p14z = T14.m23;

        float r = Mathf.Sqrt(p14x * p14x + p14z * p14z);

        // cosine rule: c3 = (r² - a1² - a2²) / (2*a1*a2)
        float a1 = a[1]; // -0.24355
        float a2 = a[2]; // -0.2132

        float c3_num = r * r - a1 * a1 - a2 * a2;
        float c3_den = 2f * a1 * a2;

        if (Mathf.Abs(c3_den) < EPSILON) return null;

        float c3 = c3_num / c3_den;
        if (Mathf.Abs(c3) > 1f + EPSILON) return null; // unreachable

        c3 = Mathf.Clamp(c3, -1f, 1f);

        float theta3_A = Mathf.Acos(c3);
        float theta3_B = -Mathf.Acos(c3); // elbow flip

        // --- θ2 (from θ3) ------------------------------------------------
        float theta2_A = SolveTheta2(p14x, p14z, a1, a2, theta3_A);
        float theta2_B = SolveTheta2(p14x, p14z, a1, a2, theta3_B);

        // --- θ4 (from T14 after removing T12 and T23) --------------------
        float theta4_A = SolveTheta4(T14, theta2_A, theta3_A);
        float theta4_B = SolveTheta4(T14, theta2_B, theta3_B);

        // Two elbow candidates — pick closest to current configuration
        float[] candA = new float[] { theta1, theta2_A, theta3_A, theta4_A, theta5, theta6 };
        float[] candB = new float[] { theta1, theta2_B, theta3_B, theta4_B, theta5, theta6 };

        bool validA = IsWithinLimits(candA);
        bool validB = IsWithinLimits(candB);

        if (!validA && !validB) return null;
        if (!validA) return candB;
        if (!validB) return candA;

        // Both valid — pick closest to current
        return JointChangeCost(current, candA) <= JointChangeCost(current, candB)
               ? candA : candB;
    }

    // -------------------------------------------------------------------------
    // Solve θ2 given θ3 and the target position in the 1-4 frame
    // -------------------------------------------------------------------------
    static float SolveTheta2(float px, float pz, float a1, float a2, float theta3)
    {
        float s3 = Mathf.Sin(theta3);
        float c3 = Mathf.Cos(theta3);

        // px = a1*c2 + a2*c(2+3)
        // pz = a1*s2 + a2*s(2+3)
        // Solve for θ2 using atan2
        float k1 = a1 + a2 * c3;
        float k2 = a2 * s3;

        return Mathf.Atan2(pz, px) - Mathf.Atan2(k2, k1);
    }

    // -------------------------------------------------------------------------
    // Solve θ4 given T14 and known θ2, θ3
    // -------------------------------------------------------------------------
    static float SolveTheta4(Matrix4x4 T14, float theta2, float theta3)
    {
        Matrix4x4 T12 = DHMatrix(alpha[1], a[1], d[1], theta2);
        Matrix4x4 T23 = DHMatrix(alpha[2], a[2], d[2], theta3);
        Matrix4x4 T13 = T12 * T23;
        Matrix4x4 T13_inv = T13.inverse;
        Matrix4x4 T34 = T13_inv * T14;

        // θ4 is the rotation around Z-axis of joint 4
        return Mathf.Atan2(T34.m10, T34.m00);
    }

    // -------------------------------------------------------------------------
    // Standard DH transformation matrix
    // T = Rz(θ) * Tz(d) * Tx(a) * Rx(α)
    // -------------------------------------------------------------------------
    static Matrix4x4 DHMatrix(float alpha_i, float a_i, float d_i, float theta_i)
    {
        float ct = Mathf.Cos(theta_i);
        float st = Mathf.Sin(theta_i);
        float ca = Mathf.Cos(alpha_i);
        float sa = Mathf.Sin(alpha_i);

        // Row-major layout matching Unity's Matrix4x4 (column-major storage,
        // but SetRow fills correctly)
        var m = new Matrix4x4();
        m.SetRow(0, new Vector4(ct, -st, 0f, a_i));
        m.SetRow(1, new Vector4(st * ca, ct * ca, -sa, -sa * d_i));
        m.SetRow(2, new Vector4(st * sa, ct * sa, ca, ca * d_i));
        m.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
        return m;
    }

    // -------------------------------------------------------------------------
    // Build 4x4 matrix from position + quaternion
    // -------------------------------------------------------------------------
    static Matrix4x4 MatFromPosRot(Vector3 pos, Quaternion rot)
    {
        Matrix4x4 m = Matrix4x4.identity;
        // Rotation part
        m.SetColumn(0, new Vector4(
            1f - 2f * (rot.y * rot.y + rot.z * rot.z),
            2f * (rot.x * rot.y + rot.z * rot.w),
            2f * (rot.x * rot.z - rot.y * rot.w),
            0f));
        m.SetColumn(1, new Vector4(
            2f * (rot.x * rot.y - rot.z * rot.w),
            1f - 2f * (rot.x * rot.x + rot.z * rot.z),
            2f * (rot.y * rot.z + rot.x * rot.w),
            0f));
        m.SetColumn(2, new Vector4(
            2f * (rot.x * rot.z + rot.y * rot.w),
            2f * (rot.y * rot.z - rot.x * rot.w),
            1f - 2f * (rot.x * rot.x + rot.y * rot.y),
            0f));
        // Translation part
        m.SetColumn(3, new Vector4(pos.x, pos.y, pos.z, 1f));
        return m;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Normalise angle to [-π, π]</summary>
    static float NormaliseAngle(float a)
    {
        while (a > Mathf.PI) a -= 2f * Mathf.PI;
        while (a < -Mathf.PI) a += 2f * Mathf.PI;
        return a;
    }

    /// <summary>Pick whichever of b or c is closer to reference a</summary>
    static float PickClosest(float reference, float b, float c)
    {
        return Mathf.Abs(NormaliseAngle(b - reference)) <=
               Mathf.Abs(NormaliseAngle(c - reference)) ? b : c;
    }

    /// <summary>Sum of squared joint angle changes — used to pick smoothest solution</summary>
    static float JointChangeCost(float[] current, float[] candidate)
    {
        float cost = 0f;
        for (int i = 0; i < 6; i++)
        {
            float d = NormaliseAngle(candidate[i] - current[i]);
            cost += d * d;
        }
        return cost;
    }

    /// <summary>Check all 6 joints are within hardware limits</summary>
    static bool IsWithinLimits(float[] angles)
    {
        for (int i = 0; i < 6; i++)
        {
            float a = NormaliseAngle(angles[i]);
            if (a < jointMin[i] || a > jointMax[i]) return false;
        }
        return true;
    }
}