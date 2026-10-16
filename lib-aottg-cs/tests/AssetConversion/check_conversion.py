"""Black-box converter regression: two shared skins and transformed rigid gear.

Run with the workspace's tools/AssetValidation/.venv/Scripts/python.exe on Windows.
"""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import struct
import zlib

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
WORKSPACE_ROOT = ROOT.parent
sys.path.insert(0, str(WORKSPACE_ROOT / 'tools/AssetValidation'))
from verify_poses import read_glb, trs, verify


def fixture(directory):
    directory.mkdir()
    positions = [[0, 0, 0], [1, 0, 0], [0, 1, 0]]
    identity = [0, 0, 0, 1]
    rotation = [0, 0, np.sin(.3), np.cos(.3)]
    definitions = [
        ('root', -1, [3, 2, 4], rotation, [2, 2, 2]),
        ('joint0', 0, [0, .5, 0], identity, [1, 1, 1]),
        ('joint1', 1, [.5, 1, 0], identity, [1, 1, 1]),
        ('part0', 0, [.2, .3, .4], identity, [1, 1, 1]),
        ('part1', 0, [-1, .2, .1], rotation, [1.2, .7, 1.1]),
        ('weapon', 2, [.2, 0, .5], rotation, [.5, .5, .5]),
        ('hidden', 0, [0, 0, 0], identity, [1, 1, 1]),
    ]
    nodes = [dict(id=i, name=name, parent=parent, position=p, rotation=q, scale=s, activeSelf=True, activeInHierarchy=True, layer=0) for i, (name, parent, p, q, s) in enumerate(definitions)]
    globals_ = []
    for n in nodes:
        local = trs(n['position'], n['rotation'], n['scale'])
        globals_.append(globals_[n['parent']] @ local if n['parent'] >= 0 else local)
    geometry = bytearray()

    def buffer(values, components, dtype='<f4'):
        a = np.asarray(values, dtype=dtype).reshape(-1, components)
        result = dict(byteOffset=len(geometry), byteLength=a.nbytes, count=len(a), components=components, componentType='float32' if dtype == '<f4' else 'int32')
        geometry.extend(a.tobytes())
        return result

    meshes = []
    for node in [3, 4, 5, 6]:
        mesh = dict(name=nodes[node]['name'], node=node, enabled=node != 6, skinned=node in [3, 4], positions=buffer(positions, 3), normals=buffer([[0, 0, 1]] * 3, 3), tangents=buffer([[1, 0, 0, 1]] * 3, 4), colors=buffer([], 4), uv0=buffer([[0, 0], [1, 0], [0, 1]], 2), uv1=buffer([], 2), submeshes=[buffer([0, 1, 2], 1, '<i4')], materials=[0])
        if mesh['skinned']:
            mesh.update(bones=[1, 2], sourceBoneSlots=[0, 1], meshToCharacter=globals_[node].T.flatten().tolist(), bindposes=buffer([ (np.linalg.inv(globals_[bone]) @ globals_[node]).T.flatten() for bone in [1, 2] ], 16), jointIndices=buffer([[0, 1, 0, 0]] * 3, 4, '<i4'), jointWeights=buffer([[.2, .8, 0, 0]] * 3, 4))
        meshes.append(mesh)
    (directory / 'geometry.bin').write_bytes(geometry)
    # An assembled renderer's current transform need not equal its bind-time
    # transform. Anchoring rest to this tiny transform used to shrink the model.
    nodes[3]['position'] = [0, 0, 0]
    nodes[3]['scale'] = [.01, .01, .01]
    meshes[0]['meshToCharacter'] = (globals_[0] @ trs(nodes[3]['position'], nodes[3]['rotation'], nodes[3]['scale'])).T.flatten().tolist()
    (directory / 'animations').mkdir()
    frames = []
    for time in [0, .5, 1]:
        sampled = copy.deepcopy(nodes)
        sampled[0]['position'][0] += time * 2  # root motion must survive once
        sampled[2]['rotation'] = [0, 0, np.sin(time), np.cos(time)]
        if time == .5:
            sampled[2]['rotation'] = [-v for v in sampled[2]['rotation']]  # sign discontinuity
        frames.append([time] + [v for n in sampled for field in ['position', 'rotation', 'scale'] for v in n[field]])
    (directory / 'animations/0000.bin').write_bytes(np.array(frames, '<f4').tobytes())
    clip = dict(name='fixture', clipName='fixture', animationNode=0, duration=1, frameCount=3, nodeCount=len(nodes), file='animations/0000.bin', sampleRate=2, authoredFrameRate=2, capturedPlaybackSpeed=.75, stateWrapMode='Loop', clipWrapMode='Loop', layer=0, blendMode='Blend', events=[], eventsAvailable=False)
    static_clip = dict(clip, name='static', file='animations/0001.bin')
    static_frames = [[time] + [v for n in nodes for field in ['position', 'rotation', 'scale'] for v in n[field]] for time in [0, .5, 1]]
    (directory / static_clip['file']).write_bytes(np.array(static_frames, '<f4').tobytes())
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    (directory / 'textures').mkdir()
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b'\x00\xff\xff\xff\xff')) + chunk(b'IEND', b'')
    (directory / 'textures/0000.png').write_bytes(png)
    manifest = dict(schema='libaottg.source-capture', version=1, exporterRevision=2, complete=True, binary='little-endian; float32 and int32; matrix column-major', coordinates='Unity: left-handed, Y-up, +Z-forward; source units', geometryFile='geometry.bin', source=dict(physicsGravity=[0, -9.81, 0], rigidbody={}), nodes=nodes, meshes=meshes, clips=[clip, static_clip], materials=[dict(name='fixture-material', shader='Diffuse', color=[.5, .6, .7, 1], renderQueue=2000, textures={'_MainTex': dict(texture=0, scale=[.5, 2], offset=[.2, -.3])})], textures=[dict(name='white', width=1, height=1, wrapMode='Repeat', filterMode='Bilinear', file='textures/0000.png')], colliders=[dict(node=0, type='CapsuleCollider', center=[1, 2, 3], height=2, radius=.5, direction=1, enabled=True, isTrigger=False)], expectedClipCount=2, sampleCount=6, warnings=[])
    save(directory, manifest)
    return manifest


def save(directory, manifest):
    (directory / 'manifest.json').write_text(json.dumps(manifest))


def run_converter(capture, output, *options, succeeds=True):
    dll = ROOT / 'src/LibAottg.AssetTool/bin/Release/net10.0/LibAottg.AssetTool.dll'
    result = subprocess.run(['dotnet', str(dll), str(capture), str(output), *options], text=True, capture_output=True)
    assert (result.returncode == 0) == succeeds, result.stdout + result.stderr
    if not succeeds:
        assert 'Conversion failed:' in result.stderr
        assert not (output / 'manifest.json').exists()


def main():
    subprocess.run(['dotnet', 'build', 'src/LibAottg.AssetTool', '-c', 'Release', '--nologo'], cwd=ROOT, check=True)
    with tempfile.TemporaryDirectory(prefix='libaottg-conversion-') as tmp:
        base = Path(tmp)
        capture = base / 'capture'
        manifest = fixture(capture)
        for unit in [1, .1, 2.5]:
            output = base / ('package-' + str(unit))
            run_converter(capture, output, '--meters-per-unit', str(unit))
            report = verify(capture, output)
            assert report['visibleMeshes'] == 3
            physics = json.loads((output / 'physics.json').read_text())
            assert np.allclose(physics['colliders'][0]['center'], [unit, 2 * unit, -3 * unit])
            gltf, _, accessor = read_glb(output / 'character.glb')
            root, joint = manifest['nodes'][:2]
            captured_joint = trs(root['position'], root['rotation'], root['scale']) @ trs(joint['position'], joint['rotation'], joint['scale'])
            descriptor = manifest['meshes'][0]['bindposes']
            bind = np.frombuffer((capture / 'geometry.bin').read_bytes(), '<f4', 16, descriptor['byteOffset']).reshape(4, 4).T
            vertices = np.array([[0, 0, 0, 1], [1, 0, 0, 1], [0, 1, 0, 1]])
            expected_rest = (np.diag([unit, unit, -unit, 1]) @ captured_joint @ bind @ vertices.T).T[:, :3]
            actual_rest = accessor(gltf['meshes'][0]['primitives'][0]['attributes']['POSITION'])
            assert np.max(np.abs(expected_rest - actual_rest)) < 3e-5 * unit, 'Rest must preserve the skeleton frame, even with a tiny current renderer transform'
            tangent = accessor(gltf['meshes'][0]['primitives'][0]['attributes']['TANGENT'])
            assert np.all(tangent[:, 3] == 1), 'Reflection and V flip must each flip tangent parity'
            # Validation of fixture uses the same pinned official validator.
            subprocess.run(['node', str(WORKSPACE_ROOT / 'tools/AssetValidation/validate.mjs'), str(output / 'character.glb')], check=True)
        destination = base / 'unlit'
        run_converter(capture, destination, '--unlit')
        gltf, _, _ = read_glb(destination / 'character.glb')
        assert gltf['extensionsUsed'] == ['KHR_materials_unlit'] and 'extensionsRequired' not in gltf
        before = (destination / 'character.glb').read_bytes()
        result = subprocess.run(['dotnet', str(ROOT / 'src/LibAottg.AssetTool/bin/Release/net10.0/LibAottg.AssetTool.dll'), str(capture), str(destination)], capture_output=True)
        assert result.returncode != 0 and (destination / 'character.glb').read_bytes() == before
        for name, mutate in [
            ('incomplete', lambda m: m.update(complete=False)),
            ('path-escape', lambda m: m.update(geometryFile='../outside.bin')),
            ('bad-buffer', lambda m: m['meshes'][0]['positions'].update(byteLength=4)),
            ('bad-bone', lambda m: m['meshes'][0]['bones'].__setitem__(0, 99)),
            ('bad-samples', lambda m: m['clips'][0].update(frameCount=2)),
        ]:
            bad = copy.deepcopy(manifest)
            mutate(bad)
            save(capture, bad)
            run_converter(capture, base / name, succeeds=False)
        save(capture, manifest)
        for scale in ['0', '-1', 'NaN']:
            run_converter(capture, base / ('bad-scale-' + scale), '--meters-per-unit', scale, succeeds=False)
        original = (capture / 'geometry.bin').read_bytes()
        damaged = bytearray(original)
        bind = manifest['meshes'][1]['bindposes']['byteOffset']
        np.frombuffer(damaged, '<f4', 16, bind)[12] += .3
        (capture / 'geometry.bin').write_bytes(damaged)
        run_converter(capture, base / 'inconsistent-binds', succeeds=False)
        (capture / 'geometry.bin').write_bytes(original)
        damaged = bytearray(original)
        weights = manifest['meshes'][0]['jointWeights']['byteOffset']
        np.frombuffer(damaged, '<f4', 12, weights)[0] = -1
        (capture / 'geometry.bin').write_bytes(damaged)
        run_converter(capture, base / 'negative-weight', succeeds=False)
    print('Asset conversion regression checks passed.')


if __name__ == '__main__':
    main()
