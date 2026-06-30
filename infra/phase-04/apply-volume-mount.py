#!/usr/bin/env python3
"""Story 2.4 (F4) — injeta o volume Azure Files + volumeMount no YAML de um Container App.

`az containerapp create/update --set-env-vars` não cobre volume mounts; estes precisam
ser editados no template YAML do Container App. Este helper recebe o YAML exportado por
`az containerapp show -o yaml`, adiciona (idempotentemente):

  template.volumes[]            → { name: n8n-data, storageType: AzureFile, storageName: <ACA_STORAGE_NAME> }
  template.containers[0].volumeMounts[] → { volumeName: n8n-data, mountPath: <MOUNT_PATH> }

e imprime o YAML resultante em stdout (para reaplicar com `az containerapp update --yaml`).

Uso:
  python3 apply-volume-mount.py <current-app.yaml> <ACA_STORAGE_NAME> <MOUNT_PATH>

Dependência: PyYAML (pré-instalado nos runners ubuntu-latest do GitHub Actions).
Convenção: o nome lógico do volume dentro do app é fixo em 'n8n-data'.
"""
import sys

try:
    import yaml
except ImportError:  # pragma: no cover - ambiente sem PyYAML
    sys.stderr.write("ERRO: PyYAML é necessário (pip install pyyaml).\n")
    sys.exit(2)

VOLUME_NAME = "n8n-data"


def main() -> int:
    if len(sys.argv) != 4:
        sys.stderr.write(
            "Uso: apply-volume-mount.py <current-app.yaml> <ACA_STORAGE_NAME> <MOUNT_PATH>\n"
        )
        return 2

    yaml_path, aca_storage_name, mount_path = sys.argv[1], sys.argv[2], sys.argv[3]

    with open(yaml_path, "r", encoding="utf-8") as fh:
        app = yaml.safe_load(fh)

    template = app.setdefault("properties", {}).setdefault("template", {})

    # --- volumes[] (idempotente por nome) ---
    volumes = template.setdefault("volumes", [])
    volume = next((v for v in volumes if v.get("name") == VOLUME_NAME), None)
    if volume is None:
        volume = {"name": VOLUME_NAME}
        volumes.append(volume)
    volume["storageType"] = "AzureFile"
    volume["storageName"] = aca_storage_name

    # --- containers[0].volumeMounts[] (idempotente por volumeName) ---
    containers = template.setdefault("containers", [])
    if not containers:
        sys.stderr.write("ERRO: nenhum container encontrado no template do app.\n")
        return 1
    mounts = containers[0].setdefault("volumeMounts", [])
    mount = next((m for m in mounts if m.get("volumeName") == VOLUME_NAME), None)
    if mount is None:
        mount = {"volumeName": VOLUME_NAME}
        mounts.append(mount)
    mount["mountPath"] = mount_path

    yaml.safe_dump(app, sys.stdout, sort_keys=False, default_flow_style=False)
    return 0


if __name__ == "__main__":
    sys.exit(main())
