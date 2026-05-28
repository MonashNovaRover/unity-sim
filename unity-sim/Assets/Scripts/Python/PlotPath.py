"""
Analyses for a single fun:
- Rover path over time
- Speed over time
- Heading over time
- Total distance travelled
- SPARC (to be ignored)
"""

import argparse
import os
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

# default location: Assets/PathTracking/
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_CSV_DIR = os.path.normpath(os.path.join(SCRIPT_DIR, "..", "..", "PathTracking"))

def resolve_path(filename):
    if os.path.dirname(filename):
        return filename
    return os.path.join(DEFAULT_CSV_DIR, filename)

def compute_ldlj(times, positions):
    times = np.asarray(times, dtype=float)
    positions = np.asarray(positions, dtype=float)

    if len(times) < 5 or len(positions) < 5:
        return float("nan")
    dt = np.median(np.diff(times))
    if dt <= 0:
        return float("nan")
    t_uniform = np.arange(times[0], times[-1], dt)
    if len(t_uniform) < 5:
        return float("nan")

    x_uniform = np.interp(t_uniform, times, positions[:, 0])
    z_uniform = np.interp(t_uniform, times, positions[:, 1])

    vx = np.gradient(x_uniform, dt)
    vz = np.gradient(z_uniform, dt)
    speed = np.sqrt(vx**2 + vz**2)
    v_peak = float(np.max(speed))
    if v_peak <= 1e-9:
        return float("nan")

    ax = np.gradient(vx, dt)
    az = np.gradient(vz, dt)

    jx = np.gradient(ax, dt)
    jz = np.gradient(az, dt)
    jerk_sq = jx**2 + jz**2

    integral = float(np.trapezoid(jerk_sq, t_uniform))

    duration = float(t_uniform[-1] - t_uniform[0])
    if duration <= 0 or integral <= 0:
        return float("nan")

    dimensionless_jerk = (duration**3 / v_peak**2) * integral

    return float(-np.log(dimensionless_jerk))

def compute_sparc(times, positions, fc=5.0, amp_thresh=0.05):

    times = np.asarray(times, dtype=float)
    positions = np.asarray(positions, dtype=float)

    if len(times) < 8 or len(positions) < 8:
        return float("nan")

    # Resample to uniform spacing (FFT requires this)
    dt = np.median(np.diff(times))
    if dt <= 0:
        return float("nan")
    t_uniform = np.arange(times[0], times[-1], dt)
    if len(t_uniform) < 8:
        return float("nan")

    x = np.interp(t_uniform, times, positions[:, 0])
    z = np.interp(t_uniform, times, positions[:, 1])

    # Speed profile (magnitude of velocity)
    vx = np.gradient(x, dt)
    vz = np.gradient(z, dt)
    speed = np.sqrt(vx**2 + vz**2)

    if np.max(speed) <= 1e-9:
        return float("nan")

    # Zero-pad to next power of 2 for cleaner spectrum
    N = len(speed)
    nfft = max(int(2 ** np.ceil(np.log2(N))), 16)

    # Magnitude spectrum, normalised
    spectrum = np.abs(np.fft.fft(speed, n=nfft))
    spectrum = spectrum / np.max(spectrum)

    # Frequency axis (one-sided, up to Nyquist)
    fs = 1.0 / dt
    freqs = np.fft.fftfreq(nfft, d=dt)
    pos_mask = freqs >= 0
    freqs = freqs[pos_mask]
    spectrum = spectrum[pos_mask]

    # Adaptive band: from 0 Hz up to min(fc, last freq exceeding amp_thresh)
    above_thresh = np.where(spectrum >= amp_thresh)[0]
    if len(above_thresh) == 0:
        return float("nan")
    f_cutoff_idx = above_thresh[-1]
    f_cutoff = min(freqs[f_cutoff_idx], fc)

    # Restrict to [0, f_cutoff]
    band_mask = freqs <= f_cutoff
    f_band = freqs[band_mask]
    s_band = spectrum[band_mask]

    if len(f_band) < 2:
        return float("nan")

    # Normalise frequency axis to [0, 1] so arc length is dimensionless
    f_norm = f_band / f_cutoff

    # Arc length of the normalised spectrum curve
    df = np.diff(f_norm)
    ds = np.diff(s_band)
    arc_length = float(np.sum(np.sqrt(df**2 + ds**2)))

    return -arc_length

def main():
    parser = argparse.ArgumentParser(description="Plot a recorded rover path top-down.")
    parser.add_argument("--actual", default="4_Vertical.csv",
                        help="CSV file with the recorded path (filename or full path)")
    parser.add_argument("--color-by", default="time",
                        choices=["time", "speed", "none"],
                        help="What to color the path by")
    args = parser.parse_args()

    actual_path = resolve_path(args.actual)
    print(f"Reading: {actual_path}")

    if not os.path.exists(actual_path):
        print(f"File not found: {actual_path}")
        return

    df = pd.read_csv(actual_path)

    if df.empty:
        print("CSV is empty — no samples recorded.")
        return

    pts = df[["x", "z"]].to_numpy()
    
    fig = plt.figure(figsize=(14, 9))

    # summary stats
    total_dist = float(np.sum(np.linalg.norm(np.diff(pts, axis=0), axis=1))) if len(pts) > 1 else 0.0
    duration = float(df["time"].iloc[-1] - df["time"].iloc[0]) if "time" in df.columns else 0.0
    mean_speed = float(df["speed"].mean()) if "speed" in df.columns else 0.0
    ldlj = compute_ldlj(df["time"].to_numpy(), pts) if "time" in df.columns else float("nan")
    sparc = compute_sparc(df["time"].to_numpy(), pts) if "time" in df.columns else float("nan")
    summary = (
        f"Samples:    {len(df)}\n"
        f"Duration:   {duration:.1f} s\n"
        f"Distance:   {total_dist:.1f} m\n"
        f"Mean speed: {mean_speed:.2f} m/s\n"
        f"LDLJ:       {ldlj:.2f}\n"
        f"SPARC:      {sparc:.2f}"
    )
    fig.text(0.02, 0.02, summary, fontsize=10, family="monospace",
             bbox=dict(boxstyle="round", facecolor="wheat", alpha=0.5))

    # top down plot
    ax = fig.add_subplot(2, 2, (1, 3))
    ax.hlines(y=20, xmin=9.3, xmax=15, linewidth=4, color="black")

    if args.color_by == "time" and "time" in df.columns:
        c = df["time"].to_numpy()
        cmap_label = "Time (s)"
    elif args.color_by == "speed" and "speed" in df.columns:
        c = df["speed"].to_numpy()
        cmap_label = "Speed (m/s)"
    else:
        c = None

    if c is not None:
        ax.plot(pts[:, 0], pts[:, 1], "-", color="gray", linewidth=0.5, alpha=0.4)
        sc = ax.scatter(pts[:, 0], pts[:, 1], c=c, cmap="viridis", s=12)
        fig.colorbar(sc, ax=ax, label=cmap_label, shrink=0.7)
    else:
        ax.plot(pts[:, 0], pts[:, 1], "r-", linewidth=1.5)

    # # Start / end markers
    # ax.scatter(*pts[0], color="lime", s=140, marker="o",
    #            edgecolors="black", linewidths=1.5, label="Start", zorder=5)
    # ax.scatter(*pts[-1], color="red", s=140, marker="s",
    #            edgecolors="black", linewidths=1.5, label="End", zorder=5)

    # heading arrows
    if "rotY" in df.columns and len(df) > 5:
        step = max(len(df) // 20, 1)
        for i in range(0, len(df), step):
            yaw_rad = np.deg2rad(df["rotY"].iloc[i])
            dx = np.sin(yaw_rad) * 0.4
            dz = np.cos(yaw_rad) * 0.4
            ax.arrow(pts[i, 0], pts[i, 1], dx, dz,
                     head_width=0.25, head_length=0.25,
                     fc="darkblue", ec="darkblue", alpha=0.5)

    ax.set_xlabel("X (m)")
    ax.set_ylabel("Z (m)")
    ax.set_title("Rover Path")
    ax.set_aspect("equal", adjustable="datalim")
    ax.grid(True, alpha=0.3)
    leg = ax.legend(["Start/Finish Line"], loc="best")
    leg_lines = leg.get_lines()
    plt.setp(leg_lines, linewidth=4)

    # speed plot
    ax2 = fig.add_subplot(2, 2, 2)
    ax2.axhline(mean_speed, linewidth=4, color="blue")
    leg2 = ax2.legend(["Mean Speed"], loc="best")
    leg2_lines = leg2.get_lines()
    plt.setp(leg2_lines, linewidth=4)

    if "speed" in df.columns and "time" in df.columns:
        ax2.plot(df["time"], df["speed"], color="cornflowerblue")
        ax2.set_xlabel("Time (s)")
        ax2.set_ylabel("Speed (m/s)")
        ax2.set_title("Speed vs. Time")
        ax2.grid(True, alpha=0.3)
    else:
        ax2.text(0.5, 0.5, "No speed/time data", ha="center", va="center")
        ax2.axis("off")

    # heading plot
    ax3 = fig.add_subplot(2, 2, 4)
    if "rotY" in df.columns and "time" in df.columns:
        ax3.plot(df["time"], df["rotY"], color="tomato")
        ax3.set_xlabel("Time (s)")
        ax3.set_ylabel("Heading (°)")
        ax3.set_title("Heading vs. Time")
        ax3.grid(True, alpha=0.3)
    else:
        ax3.text(0.5, 0.5, "No heading data", ha="center", va="center")
        ax3.axis("off")

    plt.tight_layout()
    plt.show()

if __name__ == "__main__":
    main()