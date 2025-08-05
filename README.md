# unity-sim
A Unity based simulator for Banksia

> Current unity version: `Unity 6.1 (6000.1.14f1)`

## Development

You'll need to get Unity through Unity Hub. 

### 1. Installing `unityhub` on NixOS

Add `pkgs.unityhub` and an appropriate IDE to your configuration.nix:

```nix
  # /etc/nixos/configuration.nix
  # ...
  home-manager.users.nova = {
    home.packages = with pkgs; [
      # ...

      # Add here:
      unityhub
      jetbrains.rider  # or another appropriate IDE
    ];
    
    #...
  };
  # ...
};
# Or you could also add it to environment.systemPackages, but it is better to do it with home-manager
```

I recommend using Jetbrains Rider for Unity development.

> [!IMPORTANT]  
> Make sure you update your nix channels (there was an important fix 5 hours before I wrote this)
> ```sh
> sudo nix-channels --update
> ```

Rebuild your system (this might take a while, I'm sorry).

```sh
sudo nixos-rebuild switch
```

<img width="33%" alt="image" src="https://github.com/user-attachments/assets/53b0e7ae-d93b-4dea-8aea-5c25f7647bed" align="right" />

> [!NOTE]  
> Unity Hub won't work through a `nix-shell`, as it needs to use `xdg-open` from your web browser to be able to sign in.

### 2. Downloading Unity

Run `unityhub`, and press the appropriate button to sign in or create an account using a web browser.

Once in, you may be prompted to download the latest version of unity. Dismiss this, and manually select a version of Unity to download that matches the current project (See the top of the README for the current version). 

On the left side-bar, select `Installs`. Then, in the top left, select `Install Editor`. Find the closest version >= to the version listed in the README, and press `Install`.

<img width="100%" height="719" alt="image" src="https://github.com/user-attachments/assets/add8c5ba-c681-4b64-9f40-df33c574e559" />

### 3. Open project

TODO
