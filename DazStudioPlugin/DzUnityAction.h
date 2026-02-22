#pragma once
#include <dzaction.h>
#include <dznode.h>
#include <dzjsonwriter.h>
#include <QtCore/qfile.h>
#include <QtCore/qtextstream.h>
#include <DzBridgeAction.h>
#include "DzUnityDialog.h"

class UnitTest_DzUnityAction;

#include "dzbridge.h"

class DzUnityAction : public DZ_BRIDGE_NAMESPACE::DzBridgeAction {
	 Q_OBJECT
	 Q_PROPERTY(bool InstallUnityFiles READ getInstallUnityFiles WRITE setInstallUnityFiles)
	 Q_PROPERTY(bool ExportGLTF READ getExportGLTF WRITE setExportGLTF)
	 Q_PROPERTY(bool AutoGenerateLOD READ getAutoGenerateLOD WRITE setAutoGenerateLOD)
	 Q_PROPERTY(bool AutoSetupRagdoll READ getAutoSetupRagdoll WRITE setAutoSetupRagdoll)
	 Q_PROPERTY(bool AutoGenerateMorphClips READ getAutoGenerateMorphClips WRITE setAutoGenerateMorphClips)
	 Q_PROPERTY(bool AutoEnableHairPhysics READ getAutoEnableHairPhysics WRITE setAutoEnableHairPhysics)
public:
	DzUnityAction();

	void setInstallUnityFiles(bool arg) { m_bInstallUnityFiles = arg; }
	bool getInstallUnityFiles() { return m_bInstallUnityFiles; }

	void setExportGLTF(bool arg) { m_bExportGLTF = arg; }
	bool getExportGLTF() { return m_bExportGLTF; }

	void setAutoGenerateLOD(bool arg) { m_bAutoGenerateLOD = arg; }
	bool getAutoGenerateLOD() { return m_bAutoGenerateLOD; }

	void setAutoSetupRagdoll(bool arg) { m_bAutoSetupRagdoll = arg; }
	bool getAutoSetupRagdoll() { return m_bAutoSetupRagdoll; }

	void setAutoGenerateMorphClips(bool arg) { m_bAutoGenerateMorphClips = arg; }
	bool getAutoGenerateMorphClips() { return m_bAutoGenerateMorphClips; }

	void setAutoEnableHairPhysics(bool arg) { m_bAutoEnableHairPhysics = arg; }
	bool getAutoEnableHairPhysics() { return m_bAutoEnableHairPhysics; }

protected:
	 bool m_bInstallUnityFiles;
	 bool m_bExportGLTF;
	 bool m_bAutoGenerateLOD;
	 bool m_bAutoSetupRagdoll;
	 bool m_bAutoGenerateMorphClips;
	 bool m_bAutoEnableHairPhysics;

	 void executeAction();
	 Q_INVOKABLE bool createUI();
	 Q_INVOKABLE void writeConfiguration();
	 Q_INVOKABLE void setExportOptions(DzFileIOSettings& ExportOptions);
	 Q_INVOKABLE QString createUnityFiles(bool replace = true);
	 QString readGuiRootFolder();

#ifdef UNITTEST_DZBRIDGE
	friend class UnitTest_DzUnityAction;
#endif

};
