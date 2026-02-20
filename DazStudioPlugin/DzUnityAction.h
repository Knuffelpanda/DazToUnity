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
public:
	DzUnityAction();

	void setInstallUnityFiles(bool arg) { m_bInstallUnityFiles = arg; }
	bool getInstallUnityFiles() { return m_bInstallUnityFiles; }

	void setExportGLTF(bool arg) { m_bExportGLTF = arg; }
	bool getExportGLTF() { return m_bExportGLTF; }

protected:
	 bool m_bInstallUnityFiles;
	 bool m_bExportGLTF;

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
