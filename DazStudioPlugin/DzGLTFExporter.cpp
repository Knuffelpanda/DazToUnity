// DzGLTFExporter.cpp
// Self-contained GLB (binary glTF 2.0) exporter for DazToUnity.
// No external glTF library required.
//
// GLB layout:
//   [12-byte header][JSON chunk][BIN chunk]
//
// Each chunk:
//   uint32 chunkLength | uint32 chunkType | byte[chunkLength] data
//
// JSON chunkType = 0x4E4F534A ('JSON')
// BIN  chunkType = 0x004E4942 ('BIN\0')

#include "DzGLTFExporter.h"

#include <dznode.h>
#include <dzobject.h>
#include <dzshape.h>
#include <dzmaterial.h>
#include <dzimageproperty.h>
#include <dzcolorproperty.h>
#include <dznumericproperty.h>
#include <dztexture.h>

#include "dzfacetshape.h"
#include "dzfacetmesh.h"
#include "dzfacegroup.h"
#include "dzmap.h"

#include <QtCore/qfile.h>
#include <QtCore/qfileinfo.h>
#include <QtGui/qcolor.h>

#include <cfloat>
#include <cmath>
#include <cstring>

// SDK type aliases used below:
//   DzPnt3  = typedef float DzPnt3[3]   (x==[0], y==[1], z==[2])
//   DzPnt2  = typedef float DzPnt2[2]   (u==[0], v==[1])
//   DzFacet fields: m_vertIdx[4], m_uvwIdx[4], m_normIdx[4]

// ---------------------------------------------------------------------------
// Construction
// ---------------------------------------------------------------------------

DzGLTFExporter::DzGLTFExporter()
    : m_fScale(0.01f)   // Daz cm -> glTF m
{
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

bool DzGLTFExporter::exportGLB(DzNode* node, const QString& outputPath)
{
    if (!node) {
        m_sLastError = "exportGLB: null node";
        return false;
    }

    QVector<GltfPrimData> prims;
    if (!buildPrimitives(node, prims))
        return false;

    if (prims.isEmpty()) {
        m_sLastError = "exportGLB: no geometry found on node";
        return false;
    }

    QByteArray glbData = buildGLB(prims, node->getLabel());

    QFile file(outputPath);
    if (!file.open(QIODevice::WriteOnly)) {
        m_sLastError = QString("exportGLB: cannot open '%1'").arg(outputPath);
        return false;
    }
    file.write(glbData);
    file.close();
    return true;
}

// ---------------------------------------------------------------------------
// Mesh extraction
// ---------------------------------------------------------------------------

bool DzGLTFExporter::buildPrimitives(DzNode* node,
                                      QVector<GltfPrimData>& outPrims)
{
    DzObject* obj = node->getObject();
    if (!obj) {
        m_sLastError = "buildPrimitives: node has no object";
        return false;
    }

    DzShape* shape = obj->getCurrentShape();
    DzFacetShape* facetShape = qobject_cast<DzFacetShape*>(shape);
    if (!facetShape) {
        m_sLastError = "buildPrimitives: shape is not a DzFacetShape";
        return false;
    }

    DzFacetMesh* mesh = facetShape->getFacetMesh();
    if (!mesh || mesh->getNumVertices() == 0) {
        m_sLastError = "buildPrimitives: no facet mesh / empty mesh";
        return false;
    }

    // --- vertex positions ---
    // DzPnt3 = typedef float DzPnt3[3]; access as srcPos[i][0..2]
    int numVerts = mesh->getNumVertices();
    const DzPnt3* srcPos = mesh->getVerticesPtr();

    // --- UV coordinates via DzMap (first UV set) ---
    int numUVs = 0;
    const DzPnt2* srcUVs = nullptr;
    DzMap* uvMap = mesh->getUVs();
    if (uvMap) {
        numUVs = uvMap->getNumValues();
        srcUVs = uvMap->getPnt2ArrayPtr();
    }

    // --- facets ---
    int numFacets = mesh->getNumFacets();
    const DzFacet* facets = mesh->getFacetsPtr();

    // --- material groups ---
    int numGroups    = mesh->getNumMaterialGroups();
    int numShapeMats = shape->getNumMaterials();

    // Build one GltfPrimData per material group
    for (int g = 0; g < numGroups; ++g)
    {
        DzMaterialFaceGroup* group = mesh->getMaterialGroup(g);
        if (!group || group->count() == 0)
            continue;

        GltfPrimData prim;
        prim.materialName    = group->getName();
        prim.baseColor[0]    = 1.0f;
        prim.baseColor[1]    = 1.0f;
        prim.baseColor[2]    = 1.0f;
        prim.baseColor[3]    = 1.0f;
        prim.metallicFactor  = 0.0f;
        prim.roughnessFactor = 0.5f;

        // Find matching DzMaterial by name
        for (int mi = 0; mi < numShapeMats; ++mi) {
            DzMaterial* mat = shape->getMaterial(mi);
            if (mat && mat->getName() == prim.materialName) {
                extractMaterial(mat, prim);
                break;
            }
        }

        // Triangulate faces in this group
        int numFaces    = group->count();
        const int* faceIdx = group->getIndicesPtr();

        for (int f = 0; f < numFaces; ++f)
        {
            int fi = faceIdx[f];
            if (fi < 0 || fi >= numFacets)
                continue;

            const DzFacet& face = facets[fi];

            // DzFacet fields: m_vertIdx[4], m_uvwIdx[4]
            // m_vertIdx[3] == -1 means triangle; >= 0 means quad
            bool isQuad  = (face.m_vertIdx[3] >= 0);
            int triCount = isQuad ? 2 : 1;

            // Two triangle fans from the quad: (0,1,2) and (0,2,3)
            static const int triMap[2][3] = { {0,1,2}, {0,2,3} };

            for (int t = 0; t < triCount; ++t)
            {
                // Collect the 3 vertex positions (for flat normal computation)
                float pa[3], pb[3], pc[3];
                float* pts[3] = { pa, pb, pc };
                for (int v = 0; v < 3; ++v) {
                    int vi  = triMap[t][v];
                    int idx = face.m_vertIdx[vi];
                    if (idx < 0 || idx >= numVerts) idx = 0;
                    pts[v][0] = srcPos[idx][0] * m_fScale;
                    pts[v][1] = srcPos[idx][1] * m_fScale;
                    pts[v][2] = srcPos[idx][2] * m_fScale;
                }

                float n[3];
                computeFlatNormal(pa, pb, pc, n);

                for (int v = 0; v < 3; ++v)
                {
                    int vi    = triMap[t][v];
                    int vIdx  = face.m_vertIdx[vi];
                    int uvIdx = (srcUVs && face.m_uvwIdx[vi] >= 0)
                                    ? face.m_uvwIdx[vi] : -1;

                    if (vIdx < 0 || vIdx >= numVerts) vIdx = 0;

                    // Position (DzPnt3 == float[3])
                    prim.positions.append(srcPos[vIdx][0] * m_fScale);
                    prim.positions.append(srcPos[vIdx][1] * m_fScale);
                    prim.positions.append(srcPos[vIdx][2] * m_fScale);

                    // Flat normal
                    prim.normals.append(n[0]);
                    prim.normals.append(n[1]);
                    prim.normals.append(n[2]);

                    // UV: glTF origin is top-left, Daz is bottom-left -> flip V
                    // DzPnt2 == float[2]
                    if (uvIdx >= 0 && uvIdx < numUVs) {
                        prim.texcoords.append(srcUVs[uvIdx][0]);
                        prim.texcoords.append(1.0f - srcUVs[uvIdx][1]);
                    } else {
                        prim.texcoords.append(0.0f);
                        prim.texcoords.append(0.0f);
                    }
                }
            }
        }

        if (!prim.positions.isEmpty())
            outPrims.append(prim);
    }

    return true;
}

void DzGLTFExporter::extractMaterial(DzMaterial* mat, GltfPrimData& prim)
{
    if (!mat) return;

    // Base colour
    DzProperty* diffProp = mat->findProperty("Diffuse Color", false);
    if (diffProp) {
        DzColorProperty* colProp = qobject_cast<DzColorProperty*>(diffProp);
        if (colProp) {
            QColor c = colProp->getColorValue();
            prim.baseColor[0] = (float)c.redF();
            prim.baseColor[1] = (float)c.greenF();
            prim.baseColor[2] = (float)c.blueF();
            prim.baseColor[3] = 1.0f;
        }
        DzImageProperty* imgProp = qobject_cast<DzImageProperty*>(diffProp);
        if (imgProp && imgProp->getValue())
            prim.baseColorTexturePath = imgProp->getValue()->getFilename();
    }

    // Metallic
    DzProperty* metalProp = mat->findProperty("Metallic Weight", false);
    if (!metalProp) metalProp = mat->findProperty("Metallicity", false);
    if (metalProp) {
        DzNumericProperty* np = qobject_cast<DzNumericProperty*>(metalProp);
        if (np) prim.metallicFactor = (float)np->getDoubleValue();
    }

    // Roughness (Iray calls it "Glossy Roughness")
    DzProperty* roughProp = mat->findProperty("Glossy Roughness", false);
    if (!roughProp) roughProp = mat->findProperty("Roughness", false);
    if (roughProp) {
        DzNumericProperty* np = qobject_cast<DzNumericProperty*>(roughProp);
        if (np) prim.roughnessFactor = (float)np->getDoubleValue();
    }

    // Normal map
    DzProperty* normProp = mat->findProperty("Normal Map", false);
    if (!normProp) normProp = mat->findProperty("normal map", false);
    if (normProp) {
        DzImageProperty* ip = qobject_cast<DzImageProperty*>(normProp);
        if (ip && ip->getValue())
            prim.normalTexturePath = ip->getValue()->getFilename();
    }
}

// ---------------------------------------------------------------------------
// GLB serialisation
// ---------------------------------------------------------------------------

QByteArray DzGLTFExporter::buildGLB(const QVector<GltfPrimData>& prims,
                                     const QString& nodeName)
{
    // ---- 1. Build binary buffer ----------------------------------------
    struct AccessorMeta {
        quint32 byteOffset;
        quint32 byteLength;
        int     count;
        float   minXYZ[3];
        float   maxXYZ[3];
        bool    hasMinMax;
    };

    QVector<AccessorMeta> posAcc, normAcc, uvAcc;
    QByteArray binBuf;

    for (int p = 0; p < prims.size(); ++p)
    {
        const GltfPrimData& prim = prims[p];
        int vertCount = prim.positions.size() / 3;

        // POSITION
        {
            AccessorMeta am;
            am.byteOffset = (quint32)binBuf.size();
            am.count      = vertCount;
            am.hasMinMax  = true;
            am.minXYZ[0] = am.minXYZ[1] = am.minXYZ[2] =  FLT_MAX;
            am.maxXYZ[0] = am.maxXYZ[1] = am.maxXYZ[2] = -FLT_MAX;
            for (int i = 0; i < prim.positions.size(); i += 3) {
                for (int j = 0; j < 3; ++j) {
                    float v = prim.positions[i+j];
                    if (v < am.minXYZ[j]) am.minXYZ[j] = v;
                    if (v > am.maxXYZ[j]) am.maxXYZ[j] = v;
                }
            }
            for (int i = 0; i < prim.positions.size(); ++i)
                appendFloat32LE(binBuf, prim.positions[i]);
            am.byteLength = (quint32)binBuf.size() - am.byteOffset;
            posAcc.append(am);
        }

        // NORMAL
        {
            AccessorMeta am;
            am.byteOffset = (quint32)binBuf.size();
            am.count      = vertCount;
            am.hasMinMax  = false;
            for (int i = 0; i < prim.normals.size(); ++i)
                appendFloat32LE(binBuf, prim.normals[i]);
            am.byteLength = (quint32)binBuf.size() - am.byteOffset;
            normAcc.append(am);
        }

        // TEXCOORD_0
        {
            AccessorMeta am;
            am.byteOffset = (quint32)binBuf.size();
            am.count      = prim.texcoords.size() / 2;
            am.hasMinMax  = false;
            for (int i = 0; i < prim.texcoords.size(); ++i)
                appendFloat32LE(binBuf, prim.texcoords[i]);
            am.byteLength = (quint32)binBuf.size() - am.byteOffset;
            uvAcc.append(am);
        }
    }

    // Pad BIN to 4-byte boundary
    QByteArray binPadded = padTo4(binBuf, '\0');

    // ---- 2. Collect unique image paths -----------------------------------
    QVector<QString> imagePaths;
    QVector<int>     baseColorTexIdx(prims.size(), -1);
    QVector<int>     normalTexIdx(prims.size(), -1);

    for (int p = 0; p < prims.size(); ++p) {
        // base colour texture
        if (!prims[p].baseColorTexturePath.isEmpty()) {
            int found = -1;
            for (int i = 0; i < imagePaths.size(); ++i)
                if (imagePaths[i] == prims[p].baseColorTexturePath) { found = i; break; }
            if (found < 0) { found = imagePaths.size(); imagePaths.append(prims[p].baseColorTexturePath); }
            baseColorTexIdx[p] = found;
        }
        // normal texture
        if (!prims[p].normalTexturePath.isEmpty()) {
            int found = -1;
            for (int i = 0; i < imagePaths.size(); ++i)
                if (imagePaths[i] == prims[p].normalTexturePath) { found = i; break; }
            if (found < 0) { found = imagePaths.size(); imagePaths.append(prims[p].normalTexturePath); }
            normalTexIdx[p] = found;
        }
    }

    // ---- 3. Build JSON ---------------------------------------------------
    QString json;
    json += "{\n";

    // asset
    json += "  \"asset\": { \"version\": \"2.0\", \"generator\": \"DazToUnity Bridge\" },\n";

    // scene / scenes / nodes
    json += "  \"scene\": 0,\n";
    json += "  \"scenes\": [ { \"nodes\": [0] } ],\n";
    json += QString("  \"nodes\": [ { \"name\": \"%1\", \"mesh\": 0 } ],\n")
                .arg(nodeName.isEmpty() ? "Root" : nodeName);

    // meshes
    json += "  \"meshes\": [ { \"name\": \"Mesh\", \"primitives\": [\n";
    for (int p = 0; p < prims.size(); ++p) {
        int baseAcc = p * 3;
        json += "    {\n";
        json += QString("      \"attributes\": { \"POSITION\": %1, \"NORMAL\": %2, \"TEXCOORD_0\": %3 },\n")
                    .arg(baseAcc).arg(baseAcc+1).arg(baseAcc+2);
        json += QString("      \"material\": %1,\n").arg(p);
        json += "      \"mode\": 4\n";       // TRIANGLES
        json += (p < prims.size()-1) ? "    },\n" : "    }\n";
    }
    json += "  ] } ],\n";

    // accessors + bufferViews (one bufferView per accessor)
    json += "  \"accessors\": [\n";
    for (int p = 0; p < prims.size(); ++p) {
        // POSITION
        const AccessorMeta& pa = posAcc[p];
        json += "    {\n";
        json += QString("      \"bufferView\": %1,\n").arg(p*3);
        json += "      \"byteOffset\": 0,\n";
        json += "      \"componentType\": 5126,\n";  // FLOAT
        json += QString("      \"count\": %1,\n").arg(pa.count);
        json += "      \"type\": \"VEC3\",\n";
        json += QString("      \"min\": [%1, %2, %3],\n")
                    .arg(jsonFloat(pa.minXYZ[0])).arg(jsonFloat(pa.minXYZ[1])).arg(jsonFloat(pa.minXYZ[2]));
        json += QString("      \"max\": [%1, %2, %3]\n")
                    .arg(jsonFloat(pa.maxXYZ[0])).arg(jsonFloat(pa.maxXYZ[1])).arg(jsonFloat(pa.maxXYZ[2]));
        json += "    },\n";

        // NORMAL
        const AccessorMeta& na = normAcc[p];
        json += "    {\n";
        json += QString("      \"bufferView\": %1,\n").arg(p*3+1);
        json += "      \"byteOffset\": 0,\n";
        json += "      \"componentType\": 5126,\n";
        json += QString("      \"count\": %1,\n").arg(na.count);
        json += "      \"type\": \"VEC3\"\n";
        json += "    },\n";

        // TEXCOORD_0
        const AccessorMeta& ua = uvAcc[p];
        bool lastAccessor = (p == prims.size()-1);
        json += "    {\n";
        json += QString("      \"bufferView\": %1,\n").arg(p*3+2);
        json += "      \"byteOffset\": 0,\n";
        json += "      \"componentType\": 5126,\n";
        json += QString("      \"count\": %1,\n").arg(ua.count);
        json += "      \"type\": \"VEC2\"\n";
        json += lastAccessor ? "    }\n" : "    },\n";
    }
    json += "  ],\n";

    // bufferViews
    json += "  \"bufferViews\": [\n";
    for (int p = 0; p < prims.size(); ++p) {
        bool lastPrim = (p == prims.size()-1);
        // position BV
        json += "    {\n";
        json += "      \"buffer\": 0,\n";
        json += QString("      \"byteOffset\": %1,\n").arg(posAcc[p].byteOffset);
        json += QString("      \"byteLength\": %1,\n").arg(posAcc[p].byteLength);
        json += "      \"target\": 34962\n";
        json += "    },\n";
        // normal BV
        json += "    {\n";
        json += "      \"buffer\": 0,\n";
        json += QString("      \"byteOffset\": %1,\n").arg(normAcc[p].byteOffset);
        json += QString("      \"byteLength\": %1,\n").arg(normAcc[p].byteLength);
        json += "      \"target\": 34962\n";
        json += "    },\n";
        // uv BV
        json += "    {\n";
        json += "      \"buffer\": 0,\n";
        json += QString("      \"byteOffset\": %1,\n").arg(uvAcc[p].byteOffset);
        json += QString("      \"byteLength\": %1,\n").arg(uvAcc[p].byteLength);
        json += "      \"target\": 34962\n";
        json += lastPrim ? "    }\n" : "    },\n";
    }
    json += "  ],\n";

    // images
    if (!imagePaths.isEmpty()) {
        json += "  \"images\": [\n";
        for (int i = 0; i < imagePaths.size(); ++i) {
            QString basename = QFileInfo(imagePaths[i]).fileName();
            json += QString("    { \"uri\": \"%1\" }").arg(basename);
            json += (i < imagePaths.size()-1) ? ",\n" : "\n";
        }
        json += "  ],\n";

        // textures (one per image)
        json += "  \"textures\": [\n";
        for (int i = 0; i < imagePaths.size(); ++i) {
            json += QString("    { \"source\": %1 }").arg(i);
            json += (i < imagePaths.size()-1) ? ",\n" : "\n";
        }
        json += "  ],\n";
    }

    // materials
    json += "  \"materials\": [\n";
    for (int p = 0; p < prims.size(); ++p) {
        const GltfPrimData& pr = prims[p];
        json += "    {\n";
        json += QString("      \"name\": \"%1\",\n").arg(pr.materialName);
        json += "      \"pbrMetallicRoughness\": {\n";
        json += QString("        \"baseColorFactor\": [%1, %2, %3, %4],\n")
                    .arg(jsonFloat(pr.baseColor[0])).arg(jsonFloat(pr.baseColor[1]))
                    .arg(jsonFloat(pr.baseColor[2])).arg(jsonFloat(pr.baseColor[3]));
        if (baseColorTexIdx[p] >= 0)
            json += QString("        \"baseColorTexture\": { \"index\": %1 },\n")
                        .arg(baseColorTexIdx[p]);
        json += QString("        \"metallicFactor\": %1,\n").arg(jsonFloat(pr.metallicFactor));
        json += QString("        \"roughnessFactor\": %1\n").arg(jsonFloat(pr.roughnessFactor));
        json += "      }";
        if (normalTexIdx[p] >= 0)
            json += QString(",\n      \"normalTexture\": { \"index\": %1 }").arg(normalTexIdx[p]);
        json += "\n    }";
        json += (p < prims.size()-1) ? ",\n" : "\n";
    }
    json += "  ],\n";

    // buffer
    json += QString("  \"buffers\": [ { \"byteLength\": %1 } ]\n").arg(binPadded.size());
    json += "}\n";

    // ---- 4. Assemble GLB -------------------------------------------------
    QByteArray jsonBytes  = json.toUtf8();
    QByteArray jsonPadded = padTo4(jsonBytes, ' ');

    quint32 totalLen = 12
                     + 8 + (quint32)jsonPadded.size()
                     + (binPadded.isEmpty() ? 0 : 8 + (quint32)binPadded.size());

    QByteArray glb;
    appendUint32LE(glb, 0x46546C67u);     // magic 'glTF'
    appendUint32LE(glb, 2u);              // version
    appendUint32LE(glb, totalLen);        // total length

    // JSON chunk
    appendUint32LE(glb, (quint32)jsonPadded.size());
    appendUint32LE(glb, 0x4E4F534Au);     // 'JSON'
    glb.append(jsonPadded);

    // BIN chunk
    if (!binPadded.isEmpty()) {
        appendUint32LE(glb, (quint32)binPadded.size());
        appendUint32LE(glb, 0x004E4942u); // 'BIN\0'
        glb.append(binPadded);
    }

    return glb;
}

// ---------------------------------------------------------------------------
// Geometry helpers
// ---------------------------------------------------------------------------

void DzGLTFExporter::computeFlatNormal(const float* a, const float* b,
                                        const float* c, float* outN)
{
    float u[3] = { b[0]-a[0], b[1]-a[1], b[2]-a[2] };
    float v[3] = { c[0]-a[0], c[1]-a[1], c[2]-a[2] };
    outN[0] = u[1]*v[2] - u[2]*v[1];
    outN[1] = u[2]*v[0] - u[0]*v[2];
    outN[2] = u[0]*v[1] - u[1]*v[0];
    float len = std::sqrt(outN[0]*outN[0] + outN[1]*outN[1] + outN[2]*outN[2]);
    if (len > 1e-8f) { outN[0]/=len; outN[1]/=len; outN[2]/=len; }
    else              { outN[0]=0.0f; outN[1]=1.0f; outN[2]=0.0f; }
}

// ---------------------------------------------------------------------------
// Binary helpers
// ---------------------------------------------------------------------------

void DzGLTFExporter::appendFloat32LE(QByteArray& buf, float v)
{
    quint32 bits;
    memcpy(&bits, &v, sizeof(bits));
    appendUint32LE(buf, bits);
}

void DzGLTFExporter::appendUint32LE(QByteArray& buf, quint32 v)
{
    buf.append((char)( v        & 0xFF));
    buf.append((char)((v >>  8) & 0xFF));
    buf.append((char)((v >> 16) & 0xFF));
    buf.append((char)((v >> 24) & 0xFF));
}

QByteArray DzGLTFExporter::padTo4(const QByteArray& data, char padByte)
{
    QByteArray result = data;
    int rem = result.size() % 4;
    if (rem != 0) result.append(QByteArray(4 - rem, padByte));
    return result;
}

// ---------------------------------------------------------------------------
// JSON helpers
// ---------------------------------------------------------------------------

QString DzGLTFExporter::jsonFloat(float v)
{
    return QString::number((double)v, 'f', 6);
}

QString DzGLTFExporter::jsonVec3(float x, float y, float z)
{
    return QString("[%1,%2,%3]").arg(jsonFloat(x)).arg(jsonFloat(y)).arg(jsonFloat(z));
}
