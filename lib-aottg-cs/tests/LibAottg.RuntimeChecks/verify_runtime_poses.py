"""Compare actual C# runtime deformation with the original Unity capture, using float64 math."""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import numpy as np

WORKSPACE = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(WORKSPACE / 'tools/AssetValidation'))
from verify_poses import read_glb, trs


def verify(capture_path, package_path, runtime_path):
    capture_path, package_path, runtime_path = map(Path, (capture_path, package_path, runtime_path))
    capture = json.loads((capture_path / 'manifest.json').read_text())
    package = json.loads((package_path / 'manifest.json').read_text())
    runtime = json.loads((runtime_path / 'runtime-poses.json').read_text())
    assert runtime['schema'] == 'libaottg.runtime-pose-check' and runtime['version'] == 1
    assert runtime['sha256'] == package['sha256'] == hashlib.sha256((package_path / 'character.glb').read_bytes()).hexdigest()
    gltf, _, _ = read_glb(package_path / 'character.glb')
    geometry = (capture_path / capture['geometryFile']).read_bytes()
    source_meshes = {m['node']: m for m in capture['meshes']}
    binary = np.memmap(runtime_path / 'runtime-poses.bin', dtype='<f4', mode='r')
    conversion = np.diag([package['metersPerSourceUnit'], package['metersPerSourceUnit'], -package['metersPerSourceUnit'], 1])

    def attribute(mesh, name):
        a = mesh[name]
        return np.frombuffer(geometry, '<f4' if a['componentType'] == 'float32' else '<i4', a['count'] * a['components'], a['byteOffset']).reshape(a['count'], a['components']).astype(np.float64)

    worst, vertices, frames_total, records = 0, 0, 0, []
    for clip, record in zip(capture['clips'], runtime['clips'], strict=True):
        assert clip['name'] == record['name'] and clip['frameCount'] == record['frames']
        frames, nodes = clip['frameCount'], len(capture['nodes'])
        sample = np.frombuffer((capture_path / clip['file']).read_bytes(), '<f4').reshape(frames, 1 + nodes * 10).astype(np.float64)
        tracks = sample[:, 1:].reshape(frames, nodes, 10)
        world = np.empty((frames, nodes, 4, 4))
        for i, node in enumerate(capture['nodes']):
            local = trs(tracks[:, i, :3], tracks[:, i, 3:7], tracks[:, i, 7:10])
            world[:, i] = world[:, node['parent']] @ local if node['parent'] >= 0 else local
        offset = record['offset'] // 4
        actual = binary[offset:offset + frames * runtime['verticesPerFrame'] * 3].reshape(frames, runtime['verticesPerFrame'], 3)
        vertex_offset, clip_worst = 0, 0
        for primitive in runtime['primitives']:
            mesh_index = gltf['nodes'][primitive['node']]['mesh']
            mesh = source_meshes[gltf['meshes'][mesh_index]['extras']['sourceNode']]
            pos = attribute(mesh, 'positions')
            pos = np.column_stack([pos, np.ones(len(pos))])
            assert len(pos) == primitive['vertices']
            if mesh['skinned']:
                binds = attribute(mesh, 'bindposes').reshape(-1, 4, 4).transpose(0, 2, 1)
                matrices = world[:, mesh['bones']] @ binds
                joints, weights = attribute(mesh, 'jointIndices').astype(int), attribute(mesh, 'jointWeights')
                weights /= weights.sum(axis=1, keepdims=True)
                expected = np.zeros((frames, len(pos), 4))
                for k in range(4):
                    expected += np.einsum('fvij,vj->fvi', matrices[:, joints[:, k]], pos) * weights[None, :, k, None]
            else:
                expected = np.einsum('fij,vj->fvi', world[:, mesh['node']], pos)
            expected = np.einsum('ij,fvj->fvi', conversion, expected)[..., :3]
            observed = actual[:, vertex_offset:vertex_offset + len(pos)]
            error = float(np.max(np.linalg.norm(expected - observed, axis=-1)))
            assert error < 3e-5 * package['metersPerSourceUnit'], (clip['name'], mesh['name'], error)
            clip_worst = max(error, clip_worst)
            vertices += frames * len(pos)
            vertex_offset += len(pos)
        assert vertex_offset == runtime['verticesPerFrame']
        worst = max(worst, clip_worst)
        frames_total += frames
        records.append({'name': clip['name'], 'frames': frames, 'maximumVertexError': clip_worst})
    report = {'passed': True, 'clips': len(records), 'frames': frames_total, 'vertexPositions': vertices,
              'maximumVertexError': worst, 'tolerance': 3e-5 * package['metersPerSourceUnit'], 'clipResults': records}
    (runtime_path / 'source-comparison.json').write_text(json.dumps(report, indent=2))
    print(f"Runtime vs Unity capture passed: {len(records)} clips, {frames_total} frames, {vertices:,} vertices; max error {worst:.9g}")


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('capture'); parser.add_argument('package'); parser.add_argument('runtime')
    args = parser.parse_args()
    verify(args.capture, args.package, args.runtime)
