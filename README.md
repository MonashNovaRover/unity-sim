# unity-sim
A Unity based simulator for Banksia

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

> [!NOTE]  
> Unity Hub won't work through a `nix-shell`, as it needs to use `xdg-open` from your web browser to be able to sign in.
