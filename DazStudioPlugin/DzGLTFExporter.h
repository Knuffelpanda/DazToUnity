#pragma once

#include <QString>
#include <QVector>
#include <QByteArray>

class DzNode;
class DzFacetMesh;
class DzShape;
class DzMaterial;

/// Per-primitive (per-material-group) geometry and material data.
struct GltfPrimData
{
    // Geometry — expanded (no shared vertices), one entry per triangle corner.
    // positions.size() == normals.size() == texcoords.size()/2 * 3
    QVector<float> positions;   // xyz, flat
    QVector<float> normals;     // xyz, flat (flat per-face normals)
    QVector<float> texcoords;   // uv,  flat (V flipped for glTF convention)

    // Material
    QString materialName;
    float   baseColor[4];              // RGBA, default 1,1,1,1
    float   metallicFactor;            // default 0
    float   roughnessFactor;           // default 0.5
    QString baseColorTexturePath;      // absolute path, empty if none
    QString normalTexturePath;         // absolute path, empty if none
};

/// Exports the selected DzNode as a GLB (binary glTF 2.0) file.
/// No external libraries required — uses a hand-written GLB serialiser.
///
/// Extensible stubs:
///   - extractSkeleton()  for bone hierarchy export
///   - extractMorphs()    for morph target (blend shape) export
///   - extractAnimations() for keyframe animation export
class DzGLTFExporter
{
public:
    DzGLTFExporter();

    /// Export @p node to @p outputPath (.glb).  Returns true on success.
    bool exportGLB(DzNode* node, const QString& outputPath);

    QString getLastError() const { return m_sLastError; }

    /// Scale factor applied to all positions. Daz Studio uses centimetres;
    /// glTF uses metres, so the default is 0.01.
    void setScaleFactor(float s) { m_fScale = s; }
    float getScaleFactor() const { return m_fScale; }

private:
    QString m_sLastError;
    float   m_fScale;

    // ---- mesh extraction ----
    bool buildPrimitives(DzNode* node, QVector<GltfPrimData>& outPrims);
    void extractMaterial(DzMaterial* mat, GltfPrimData& prim);

    // ---- GLB serialisation ----
    QByteArray buildGLB(const QVector<GltfPrimData>& prims,
                        const QString& nodeName);

    // ---- geometry helpers ----
    static void computeFlatNormal(const float* a, const float* b,
                                  const float* c, float* outN);

    // ---- binary helpers ----
    static void appendFloat32LE(QByteArray& buf, float v);
    static void appendUint32LE (QByteArray& buf, quint32 v);
    static QByteArray padTo4(const QByteArray& data, char padByte);

    // ---- JSON helpers ----
    static QString jsonFloat(float v);
    static QString jsonVec3(float x, float y, float z);
};
