Describe "UKHO.SourceDocumentService Deployment Security Tests" {  
    
    $expectedADGroups = @(
                            "AG_PCPPREIISSVC-GG", 
                            "AG_Cuiaworkspace-PRE_RW-GG")
    $adServiceAccount = "SVC_TMSdocSvcPoolPre"

    Context "Source Document Service adheres to principle of least privilege by" {      
        It "is not in any additional AD groups"{
            $actualGroups = Get-ADPrincipalGroupMembership -Identity $adServiceAccount | `
                      Select-Object -ExpandProperty name | `
                      Where-Object {$_ -ne "Domain Users"}
            $delta = $actualGroups | Where-Object {$expectedADGroups -NotContains $_}

            if($delta.Count -gt 0) {
                $errorMessage =  ("Expected groups: " + ($expectedADGroups -join ",") + " `n" `
                                  + "But got groups: " + ($actualGroups -join ","))
                throw ($errorMessage)
            }        
         } 

        It "is not a local admin on node" {
            (Get-LocalGroupMember 'Administrators').Name -contains $adUser | Should not be $true
        }        
    }
}
