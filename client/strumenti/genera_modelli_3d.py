"""Genera i due modelli 3D low-poly animati per FantaAge:
  1) client/public/modelli/duello.glb   — due milizie (verde vs rossa) che si punzecchiano/duellano
  2) client/public/modelli/paladino.glb — paladino a cavallo con pennacchio tricolore e lancia

Esecuzione:
  /opt/homebrew/bin/blender --background --python client/strumenti/genera_modelli_3d.py
"""
import math
import os
import bpy
from mathutils import Vector

OUT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "public", "modelli"))
os.makedirs(OUT_DIR, exist_ok=True)


def pulisci_scena():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 24
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 60


def mat(nome, rgba, metallic=0.15, roughness=0.55, emission=None, emission_strength=0.0):
    m = bpy.data.materials.new(nome)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None and emission_strength > 0:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return m


def crea_materiali():
    return {
        "base": mat("Base", (0.06, 0.09, 0.13, 1.0), metallic=0.2, roughness=0.65),
        "bordo": mat("Bordo", (0.22, 0.78, 0.52, 1.0), metallic=0.1, roughness=0.35, emission=(0.22, 0.78, 0.52, 1.0), emission_strength=0.35),
        "acciaio": mat("Acciaio", (0.68, 0.75, 0.84, 1.0), metallic=0.82, roughness=0.28),
        "acciaio_scuro": mat("AcciaioScuro", (0.14, 0.19, 0.26, 1.0), metallic=0.7, roughness=0.38),
        "verde": mat("Verde", (0.18, 0.76, 0.48, 1.0), metallic=0.15, roughness=0.42),
        "rosso": mat("Rosso", (0.84, 0.30, 0.32, 1.0), metallic=0.15, roughness=0.42),
        "bianco": mat("Bianco", (0.92, 0.94, 0.97, 1.0), metallic=0.05, roughness=0.45),
        "legno": mat("Legno", (0.36, 0.24, 0.15, 1.0), metallic=0.0, roughness=0.75),
        "cavallo": mat("Cavallo", (0.28, 0.21, 0.17, 1.0), metallic=0.05, roughness=0.65),
        "oro": mat("Oro", (0.86, 0.68, 0.25, 1.0), metallic=0.85, roughness=0.25),
    }


def cubo(nome, loc, scale, material, parent=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = nome
    o.scale = scale
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def cilindro(nome, loc, r, h, seg, material, parent=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=r, depth=h, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = nome
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def cono(nome, loc, r1, r2, h, seg, material, parent=None, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=seg, radius1=r1, radius2=r2, depth=h, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = nome
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def empty(nome, loc=(0, 0, 0), parent=None, rot=(0, 0, 0)):
    o = bpy.data.objects.new(nome, None)
    bpy.context.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = rot
    if parent:
        o.parent = parent
    return o


def crea_base(m, raggio=1.55):
    cilindro("BaseHex", (0, 0, -0.09), raggio, 0.16, 6, m["base"])
    cilindro("BaseRing", (0, 0, -0.01), raggio * 1.02, 0.03, 6, m["bordo"])


def crea_milizia(prefisso, colore_mat, m, pos_x, guarda_destra=True):
    rot_z = 0.0 if guarda_destra else math.pi
    root = empty(f"{prefisso}_Root", (pos_x, 0, 0), rot=(0, 0, rot_z))

    # Gambe
    cubo(f"{prefisso}_GambaSX", (-0.04, 0.13, 0.28), (0.16, 0.15, 0.56), m["acciaio_scuro"], root)
    cubo(f"{prefisso}_GambaDX", (0.04, -0.13, 0.28), (0.16, 0.15, 0.56), m["acciaio_scuro"], root)

    # Busto (pivot in vita)
    busto = empty(f"{prefisso}_Busto", (0, 0, 0.56), root)
    cubo(f"{prefisso}_Torso", (0, 0, 0.28), (0.34, 0.44, 0.56), colore_mat, busto)
    cubo(f"{prefisso}_Cintura", (0, 0, 0.06), (0.36, 0.46, 0.10), m["legno"], busto)
    cubo(f"{prefisso}_SpallaccioSX", (0, 0.26, 0.48), (0.24, 0.16, 0.16), m["acciaio"], busto)
    cubo(f"{prefisso}_SpallaccioDX", (0, -0.26, 0.48), (0.24, 0.16, 0.16), m["acciaio"], busto)

    # Testa + Elmo low-poly
    cubo(f"{prefisso}_Testa", (0, 0, 0.72), (0.28, 0.28, 0.28), m["acciaio"], busto)
    cono(f"{prefisso}_ElmoTop", (0, 0, 0.92), 0.20, 0.04, 0.18, 6, m["acciaio"], busto)
    cubo(f"{prefisso}_Visiera", (0.15, 0, 0.72), (0.04, 0.20, 0.06), m["acciaio_scuro"], busto)
    cubo(f"{prefisso}_Cresta", (-0.02, 0, 1.02), (0.22, 0.05, 0.12), colore_mat, busto)

    # Braccio sinistro + Scudo (pivot spalla)
    braccio_sx = empty(f"{prefisso}_BraccioSX", (0.04, 0.30, 0.44), busto)
    cubo(f"{prefisso}_AvambSX", (0.16, 0.02, -0.06), (0.32, 0.13, 0.13), m["acciaio_scuro"], braccio_sx)
    cubo(f"{prefisso}_Scudo", (0.36, 0.06, -0.04), (0.07, 0.42, 0.58), colore_mat, braccio_sx)
    cubo(f"{prefisso}_ScudoBordo", (0.39, 0.06, -0.04), (0.03, 0.28, 0.44), m["acciaio"], braccio_sx)

    # Braccio destro + Spada (pivot spalla)
    braccio_dx = empty(f"{prefisso}_BraccioDX", (0.04, -0.30, 0.44), busto)
    cubo(f"{prefisso}_AvambDX", (0.18, -0.02, -0.04), (0.34, 0.13, 0.13), m["acciaio_scuro"], braccio_dx)
    # Impugnatura + guardia + lama
    cilindro(f"{prefisso}_Manico", (0.34, -0.02, -0.04), 0.035, 0.22, 6, m["legno"], braccio_dx)
    cubo(f"{prefisso}_Guardia", (0.34, -0.02, 0.08), (0.06, 0.22, 0.05), m["oro"], braccio_dx)
    cubo(f"{prefisso}_Lama", (0.34, -0.02, 0.48), (0.04, 0.09, 0.74), m["acciaio"], braccio_dx)
    cono(f"{prefisso}_Punta", (0.34, -0.02, 0.90), 0.055, 0.005, 0.14, 4, m["acciaio"], braccio_dx)

    return root, busto, braccio_sx, braccio_dx


def chiave_rot(obj, frame, rx, ry, rz):
    obj.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(rz))
    obj.keyframe_insert(data_path="rotation_euler", frame=frame)


def chiave_loc(obj, frame, x, y, z):
    obj.location = (x, y, z)
    obj.keyframe_insert(data_path="location", frame=frame)


def genera_duello():
    pulisci_scena()
    m = crea_materiali()
    crea_base(m, 1.55)

    rA, bA, sxA, dxA = crea_milizia("Verde", m["verde"], m, -0.78, True)
    rB, bB, sxB, dxB = crea_milizia("Rosso", m["rosso"], m, 0.78, False)

    # Loop 60 frame: Verde attacca (1..28), Rosso contrattacca (30..58), ritorno a 60
    # --- Milizia Verde ---
    for f, x, pitch, sx_y, dx_y in [
        (1,  -0.78,  0,   0,  -25),
        (8,  -0.72, -10, -8,  -68),  # carica il colpo
        (15, -0.48,  16, 10,   42),  # affondo!
        (22, -0.68,   4,  4,   -5),
        (30, -0.78,   0,  0,  -25),
        (42, -0.88, -12, -26, -35),  # para colpo di Rosso
        (52, -0.80,  -3, -6,  -28),
        (60, -0.78,   0,  0,  -25),
    ]:
        chiave_loc(rA, f, x, 0, 0)
        chiave_rot(bA, f, 0, pitch, 0)
        chiave_rot(sxA, f, 0, sx_y, 0)
        chiave_rot(dxA, f, 0, dx_y, 0)

    # --- Milizia Rossa ---
    for f, x, pitch, sx_y, dx_y in [
        (1,  0.78,   0,   0,  -25),
        (12, 0.88, -12, -26,  -35),  # para colpo di Verde
        (22, 0.80,  -2,  -5,  -26),
        (30, 0.78,   0,   0,  -25),
        (38, 0.72, -10,  -8,  -68),  # carica contrattacco
        (45, 0.48,  16,  10,   42),  # affondo!
        (53, 0.68,   4,   4,   -5),
        (60, 0.78,   0,   0,  -25),
    ]:
        chiave_loc(rB, f, x, 0, 0)
        chiave_rot(bB, f, 0, pitch, 0)
        chiave_rot(sxB, f, 0, sx_y, 0)
        chiave_rot(dxB, f, 0, dx_y, 0)

    out = os.path.join(OUT_DIR, "duello.glb")
    bpy.ops.export_scene.gltf(
        filepath=out,
        export_format="GLB",
        export_animations=True,
        export_materials="EXPORT",
    )
    print("Esportato:", out, os.path.getsize(out), "bytes")


def genera_paladino():
    pulisci_scena()
    m = crea_materiali()
    crea_base(m, 1.45)

    root = empty("Paladino_Root", (0, 0, 0))
    corpo = empty("Cavallo_Corpo", (0, 0, 0.92), root)

    # Tronco cavallo + bardo corazzato
    cubo("Cavallo_Torso", (0, 0, 0), (1.15, 0.46, 0.44), m["cavallo"], corpo)
    cubo("Bardo", (0, 0, -0.08), (1.18, 0.50, 0.34), m["acciaio_scuro"], corpo)
    cubo("Bardo_Bordo", (0, 0, -0.25), (1.19, 0.51, 0.05), m["oro"], corpo)
    cubo("Sella", (-0.05, 0, 0.24), (0.44, 0.42, 0.10), m["legno"], corpo)

    # Collo e testa cavallo
    collo = empty("Cavallo_Collo", (0.50, 0, 0.16), corpo)
    cubo("Collo_Mesh", (0.14, 0, 0.24), (0.28, 0.24, 0.52), m["cavallo"], collo, rot=(0, math.radians(24), 0))
    cubo("Testa_Cavallo", (0.34, 0, 0.48), (0.42, 0.22, 0.22), m["cavallo"], collo, rot=(0, math.radians(32), 0))
    cubo("Frontale_Acciaio", (0.40, 0, 0.54), (0.34, 0.24, 0.08), m["acciaio"], collo, rot=(0, math.radians(32), 0))
    cubo("Criniera", (0.04, 0, 0.32), (0.08, 0.08, 0.42), m["bianco"], collo, rot=(0, math.radians(24), 0))

    # Coda
    coda = empty("Cavallo_Coda", (-0.58, 0, 0.08), corpo)
    cubo("Coda_Mesh", (-0.12, 0, -0.22), (0.12, 0.12, 0.46), m["bianco"], coda, rot=(0, math.radians(25), 0))

    # 4 Zampe articolate
    zampe = []
    for nome, lx, ly in [("AntSX", 0.42, 0.17), ("AntDX", 0.42, -0.17), ("PostSX", -0.42, 0.17), ("PostDX", -0.42, -0.17)]:
        p = empty(f"Zampa_{nome}", (lx, ly, -0.18), corpo)
        cubo(f"Zampa_{nome}_Mesh", (0, 0, -0.34), (0.14, 0.14, 0.68), m["cavallo"], p)
        cubo(f"Zoccolo_{nome}", (0, 0, -0.68), (0.16, 0.16, 0.09), m["acciaio_scuro"], p)
        zampe.append(p)

    # Cavaliere / Paladino in sella
    cav = empty("Cavaliere", (-0.06, 0, 0.28), corpo)
    cubo("Cav_Torso", (0, 0, 0.34), (0.34, 0.42, 0.56), m["acciaio"], cav)
    cubo("Cav_Mantello", (-0.22, 0, 0.28), (0.08, 0.38, 0.56), m["verde"], cav, rot=(0, math.radians(14), 0))
    cubo("Cav_Testa", (0, 0, 0.76), (0.26, 0.26, 0.26), m["acciaio"], cav)
    cono("Cav_Elmo", (0, 0, 0.96), 0.19, 0.04, 0.18, 6, m["acciaio"], cav)
    cubo("Cav_Visiera", (0.14, 0, 0.76), (0.04, 0.18, 0.06), m["acciaio_scuro"], cav)

    # Pennacchio tricolore AoE Italia (verde - bianco - rosso)
    pen = empty("Pennacchio", (-0.05, 0, 1.04), cav)
    cubo("Pen_Verde", (0.04, 0, 0.08), (0.14, 0.06, 0.16), m["verde"], pen)
    cubo("Pen_Bianco", (-0.08, 0, 0.06), (0.14, 0.06, 0.16), m["bianco"], pen)
    cubo("Pen_Rosso", (-0.20, 0, 0.02), (0.14, 0.06, 0.14), m["rosso"], pen)

    # Scudo a sinistra
    cubo("Cav_Scudo", (0.12, 0.28, 0.34), (0.07, 0.34, 0.50), m["verde"], cav, rot=(0, 0, math.radians(18)))
    cubo("Cav_ScudoCroce", (0.15, 0.29, 0.34), (0.03, 0.16, 0.34), m["bianco"], cav, rot=(0, 0, math.radians(18)))

    # Braccio destro + Lancia da torneo con gagliardetto
    braccio = empty("Cav_BraccioLancia", (0.06, -0.28, 0.46), cav)
    cubo("Cav_BraccioMesh", (0.14, 0, -0.04), (0.28, 0.12, 0.12), m["acciaio_scuro"], braccio)
    cilindro("Lancia_Asta", (0.72, 0, 0.24), 0.032, 2.05, 6, m["legno"], braccio, rot=(0, math.radians(66), 0))
    cono("Lancia_Punta", (1.68, 0, 0.67), 0.075, 0.005, 0.28, 6, m["acciaio"], braccio, rot=(0, math.radians(66), 0))
    cubo("Lancia_Bandiera", (1.18, -0.02, 0.36), (0.36, 0.03, 0.20), m["rosso"], braccio, rot=(0, math.radians(-12), 0))

    # Animazione 60 frame: trotto fiero + piccola impennata + oscillazione lancia/pennacchio
    for f, z, pitch_c, z_ant, z_post, lancia_y, pen_y in [
        (1,  0.92,  0,  18, -18,  0,   0),
        (15, 1.00, -7, -22,  20, -8,  -8),
        (30, 0.91,  2,  18, -18,  4,   4),
        (45, 1.02, -9, -24,  22, -10, -10),
        (60, 0.92,  0,  18, -18,  0,   0),
    ]:
        chiave_loc(corpo, f, 0, 0, z)
        chiave_rot(corpo, f, 0, pitch_c, 0)
        chiave_rot(collo, f, 0, -pitch_c * 0.7, 0)
        chiave_rot(zampe[0], f, 0, z_ant, 0)
        chiave_rot(zampe[1], f, 0, -z_ant, 0)
        chiave_rot(zampe[2], f, 0, z_post, 0)
        chiave_rot(zampe[3], f, 0, -z_post, 0)
        chiave_rot(braccio, f, 0, lancia_y, 0)
        chiave_rot(pen, f, 0, pen_y, 0)
        chiave_rot(coda, f, 0, -pitch_c * 1.4, 0)

    out = os.path.join(OUT_DIR, "paladino.glb")
    bpy.ops.export_scene.gltf(
        filepath=out,
        export_format="GLB",
        export_animations=True,
        export_materials="EXPORT",
    )
    print("Esportato:", out, os.path.getsize(out), "bytes")


if __name__ == "__main__":
    genera_duello()
    genera_paladino()
