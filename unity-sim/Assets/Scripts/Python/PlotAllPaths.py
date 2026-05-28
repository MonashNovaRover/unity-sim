import os
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

plt.rcParams["font.family"] = "Times New Roman"

# default location: Assets/PathTracking/
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_CSV_DIR = os.path.normpath(os.path.join(SCRIPT_DIR, "..", "..", "PathTracking"))

CONFIGS = {
    "Mast": {
        "color": "#e41a1c",  # red
        "files": [
            "1_Mast.csv",
            "2_Mast.csv",
            "3_Mast.csv",
            "Brandon_Mast.csv",
            "Denny_Mast.csv",
            "Henry_Mast.csv",
            "Joel_Mast.csv",
            "Ron_Mast.csv",
        ],
    },
    "Panoramic": {
        "color": "#377eb8",  # blue
        "files": [
            "1_Pano.csv",
            "Aiden_Pano.csv",
            "Brandon_Pano.csv",
            "Denny_Pano.csv",
            "Henry_Pano.csv",
            "Joel_Pano.csv",
            "Ron_Pano.csv",
            "Tristan_Pano.csv",
        ],
    },
    "Vertical": {
        "color": "#4daf4a",  # green
        "files": [
            "1_Vertical.csv",
            "2_Vertical.csv",
            "3_Vertical.csv",
            "4_Vertical.csv",
            "Brandon_Vertical.csv",
            "Denny_Vertical.csv",
            "Henry_Vertical.csv",
            "Joel_Vertical.csv",
        ],
    },
    "Floating": {
        "color": "#984ea3",  # purple
        "files": [
            "1_Floating.csv",
            "2_Floating.csv",
            "3_Floating.csv",
            "4_Floating.csv",
            "Brandon_Floating.csv",
            "Denny_Floating.csv",
            "Henry_Floating.csv",
            "Joel_Floating.csv",
        ],
    },
}

INDIVIDUAL_LINE_WIDTH = 1.0
INDIVIDUAL_ALPHA = 0.4          # faint background runs
MEAN_LINE_WIDTH = 3.0
MEAN_ALPHA = 1.0                # fully opaque mean
RESAMPLE_POINTS = 200           # how many points to resample each path to

def resample_path(x, z, n_points=RESAMPLE_POINTS):
    if len(x) < 2:
        return None, None

    # Cumulative arc length along the path
    dx = np.diff(x)
    dz = np.diff(z)
    seg_lengths = np.sqrt(dx**2 + dz**2)
    cum_length = np.concatenate(([0.0], np.cumsum(seg_lengths)))
    total = cum_length[-1]

    if total <= 0:
        return None, None

    # New points at equal arc-length intervals
    target = np.linspace(0, total, n_points)
    x_resampled = np.interp(target, cum_length, x)
    z_resampled = np.interp(target, cum_length, z)
    return x_resampled, z_resampled

def main():
    fig, ax = plt.subplots(figsize=(12, 10))

    for config_name, info in CONFIGS.items():
        color = info["color"]
        files = info["files"]
        legend_added = False
        resampled_runs = []  # collect for averaging

        for filename in files:
            path = os.path.join(DEFAULT_CSV_DIR, filename)
            if not os.path.exists(path):
                print(f"WARNING: file not found, skipping: {path}")
                continue

            try:
                df = pd.read_csv(path)
            except Exception as e:
                print(f"WARNING: failed to read {filename}: {e}")
                continue

            if df.empty or "x" not in df.columns or "z" not in df.columns:
                print(f"WARNING: {filename} missing x/z columns or empty, skipping")
                continue

            x = df["x"].to_numpy()
            z = df["z"].to_numpy()

            # Plot the individual run (faint)
            label = f"{config_name}" if not legend_added else None
            ax.plot(x, z,
                    color=color, linewidth=INDIVIDUAL_LINE_WIDTH,
                    alpha=INDIVIDUAL_ALPHA, label=label)
            legend_added = True

            # Resample for averaging
            xr, zr = resample_path(x, z)
            if xr is not None:
                resampled_runs.append((xr, zr))

        # Compute and plot the mean path for this config
        if len(resampled_runs) >= 2:
            xs = np.stack([r[0] for r in resampled_runs])
            zs = np.stack([r[1] for r in resampled_runs])
            mean_x = xs.mean(axis=0)
            mean_z = zs.mean(axis=0)

            ax.plot(mean_x, mean_z,
                    color=color, linewidth=MEAN_LINE_WIDTH,
                    alpha=MEAN_ALPHA, label=f"{config_name} Average",
                    solid_capstyle="round")
        elif len(resampled_runs) == 1:
            print(f"NOTE: only 1 run for {config_name}, no mean computed")
        else:
            print(f"NOTE: no valid runs for {config_name}, no mean computed")

    ax.hlines(y=20, xmin=9.3, xmax=15, linewidth=4, color="black", label="Start/Finish")
    ax.set_xlabel("X (m)", fontsize=20)
    ax.set_ylabel("Z (m)", fontsize=20)
    ax.set_title("Rover Trajectory Paths by Camera Modality", fontsize=24, pad=15, fontweight="bold")
    ax.set_aspect("equal", adjustable="datalim")
    ax.grid(True, alpha=0.3, color="black")
    ax.tick_params(axis="both", labelsize=20)

    leg = ax.legend(title="Legend", loc="upper right", borderpad=1, framealpha=0.9, fontsize=16, bbox_to_anchor=(0.98, 0.98))
    leg.get_title().set_fontweight("bold")
    leg.get_title().set_fontsize(18)
    leg.get_title().set_y(7)
    leg.get_frame().set_edgecolor("grey")
    leg_lines = leg.get_lines()
    plt.setp(leg_lines, linewidth=4)

    plt.tight_layout()

    # Save with transparent background
    output_path = os.path.join(SCRIPT_DIR, "all_runs.png")
    plt.savefig(output_path,
                transparent=True,        # transparent figure + axes background
                dpi=300,                 # high res for overlay
                bbox_inches="tight",     # trim whitespace
                pad_inches=0.1)
    print(f"Saved transparent plot to: {output_path}")

    plt.show()


if __name__ == "__main__":
    main()